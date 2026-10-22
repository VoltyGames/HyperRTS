using System;
using System.Collections.Generic;
using HyperRTS.Core;
using HyperRTS.Simulation.AI;
using HyperRTS.Simulation.Buildings;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.GameEntities;
using HyperRTS.Simulation.Match;
using HyperRTS.Simulation.Navigation;
using HyperRTS.Simulation.Orders;
using HyperRTS.Simulation.Units;
using Unity.Collections;
using Unity.Core;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.Tests
{
    /// <summary>
    /// Isolated world running every system in HyperRTS.Simulation (no rendering, input or physics), plus
    /// helpers that build entities with the same setup code the bakers use.
    /// </summary>
    public sealed class TestWorld : IDisposable
    {
        public const float FrameTime = 1f / 30f;

        public readonly World World;
        private readonly List<BlobAssetReference<HeightfieldBlob>> _terrains = new();
        private readonly List<UnityEngine.Object> _owned = new();
        private double _elapsed;

        public TestWorld()
        {
            // Statics outlive Play mode (no domain reload), so a game's last match setup must not leak into tests.
            MatchSetupRequest.Clear();
            World = new World("HyperRTS Test World");

            var simulation = typeof(UnitTag).Assembly;
            var systems = new List<Type>
            {
                typeof(SimulationSystemGroup),
                typeof(TransformSystemGroup),
                typeof(BeginSimulationEntityCommandBufferSystem),
                typeof(EndSimulationEntityCommandBufferSystem),
            };

            foreach (var type in DefaultWorldInitialization.GetAllSystems(WorldSystemFilterFlags.Default))
            {
                if (type.Assembly == simulation)
                {
                    systems.Add(type);
                }
            }

            DefaultWorldInitialization.AddSystemsToRootLevelSystemGroups(World, systems);
        }

        /// <summary>Wraps a world built elsewhere, such as a netcode host, for the setup helpers; its owner ticks it.</summary>
        public TestWorld(World world)
        {
            MatchSetupRequest.Clear();
            World = world;
        }

        public EntityManager EntityManager => World.EntityManager;

        public void Dispose()
        {
            if (World.IsCreated)
            {
                World.Dispose();
            }

            foreach (var terrain in _terrains)
            {
                terrain.Dispose();
            }

            foreach (var owned in _owned)
            {
                UnityEngine.Object.DestroyImmediate(owned);
            }
        }

        /// <summary>Hands <paramref name="asset"/> to this world, which destroys it on dispose.</summary>
        public T Own<T>(T asset) where T : UnityEngine.Object
        {
            _owned.Add(asset);
            return asset;
        }

        /// <summary>
        /// Updates the simulation group, plus the prefab registry from initialization, but not the whole
        /// initialization group, so the pushed time isn't overwritten.
        /// </summary>
        public void Tick(float deltaTime = FrameTime, int frames = 1)
        {
            for (var i = 0; i < frames; i++)
            {
                _elapsed += deltaTime;
                World.SetTime(new TimeData(_elapsed, deltaTime));
                World.GetExistingSystem<PrefabRegistrySystem>().Update(World.Unmanaged);
                World.GetExistingSystemManaged<SimulationSystemGroup>().Update();
            }
        }

        /// <summary>Ticks fixed frames until <paramref name="seconds"/> of game time have passed.</summary>
        public void Run(float seconds) => Tick(FrameTime, (int)math.ceil(seconds / FrameTime));

        /// <summary>
        /// Ticks until <paramref name="done"/> holds or <paramref name="cap"/> seconds pass. Returns whether it
        /// holds, without asserting, so callers keep their own assert message.
        /// </summary>
        public bool RunUntil(Func<bool> done, float cap)
        {
            for (var t = 0f; t < cap && !done(); t += FrameTime)
            {
                Tick();
            }

            return done();
        }

        public T Get<T>(Entity entity) where T : unmanaged, IComponentData => EntityManager.GetComponentData<T>(entity);

        public T Singleton<T>() where T : unmanaged, IComponentData
        {
            using var query = EntityManager.CreateEntityQuery(typeof(T));
            return query.GetSingleton<T>();
        }

        /// <summary>The match singleton carrying <see cref="MapSettings"/>.</summary>
        public Entity MatchEntity()
        {
            using var query = EntityManager.CreateEntityQuery(typeof(MapSettings));
            return query.GetSingletonEntity();
        }

        public bool IsEnabled<T>(Entity entity) where T : unmanaged, IComponentData, IEnableableComponent =>
            EntityManager.IsComponentEnabled<T>(entity);

        /// <summary>
        /// Creates map, rules, relations, AI tunings and one player per team entry (faction = index + 1, first is
        /// local), plus the AI map hints every AI needs: a slot per player and a direct lane (no waypoints) between
        /// enemies; <see cref="AIHintsTestKit.CreateHints"/> replaces them. Builds the nav grid, as a loaded map has
        /// before anyone commands.
        /// </summary>
        public void CreateMatch(params byte[] teams)
        {
            var match = EntityManager.CreateEntity();
            EntityManager.AddComponentData(match, new MapSettings
            {
                Min = new float2(-100f, -100f),
                Size = new float2(200f, 200f),
                NavCellSize = 1f,
                FogCellSize = 2f,
                FogOfWar = true,
            });
            EntityManager.AddComponentData(match, new MatchState());
            EntityManager.AddComponentData(match, new MatchRules
            {
                LowPowerProductionRate = 0.5f, SellRefund = 0.5f, ExperiencePerCost = 0.1f, JoinTimeout = 60f,
                StrategicAssets = true,
            });

            var relations = new FactionRelations();
            relations.Teams.Add(0);
            for (var i = 0; i < teams.Length; i++)
            {
                relations.Teams.Add(teams[i]);
                CreatePlayer((byte)(i + 1), local: i == 0);
            }

            EntityManager.AddComponentData(match, relations);
            var tunings = EntityManager.AddBuffer<AIDifficultyTuning>(match);
            foreach (AIDifficulty difficulty in Enum.GetValues(typeof(AIDifficulty)))
            {
                tunings.Add(new AITuning().ToTuning(difficulty));
            }

            this.CreateHints(AIHintsTestKit.DirectHints(teams));
            BuildNavGrid();
        }

        /// <summary>Bakes a heightfield over the test map from a height function, as Terrain Height authoring does.</summary>
        public void CreateTerrain(Func<float2, float> height, float spacing = 1f)
        {
            var min = new float2(-100f, -100f);
            var size = (int2)(200f / spacing) + 1;
            var heights = new NativeArray<float>(size.x * size.y, Allocator.Temp);
            for (var i = 0; i < heights.Length; i++)
            {
                heights[i] = height(min + new float2(i % size.x, i / size.x) * spacing);
            }

            var terrain = TerrainHeight.Create(heights, size, min, spacing, Allocator.Persistent);
            heights.Dispose();
            _terrains.Add(terrain.Blob);
            var writer = new EntityManagerWriter(EntityManager, EntityManager.CreateEntity());
            NavSetup.AddTerrain(ref writer, terrain);
            BuildNavGrid();
        }

        /// <summary>Builds the nav grid over the current map and terrain, as the map's first frames do.</summary>
        private void BuildNavGrid()
        {
            World.GetExistingSystem<NavGridSystem>().Update(World.Unmanaged);
            EntityManager.CompleteAllTrackedJobs();
        }

        public Entity Player(byte faction)
        {
            using var query = EntityManager.CreateEntityQuery(typeof(Player));
            using var players = query.ToEntityArray(Allocator.Temp);
            foreach (var player in players)
            {
                if (Get<Player>(player).Faction == faction)
                {
                    return player;
                }
            }

            throw new ArgumentException($"No player with faction {faction}.");
        }

        public void Command(byte faction, PlayerCommand command) =>
            EntityManager.GetBuffer<PlayerCommand>(Player(faction)).Add(command);

        /// <summary>Gives the unit an order through <see cref="OrderWriter"/>, as the command systems do.</summary>
        public void Issue(Entity unit, in Order order, bool queue = false)
        {
            var system = World.GetOrCreateSystem<TestOrderSystem>();
            World.Unmanaged.GetUnsafeSystemRef<TestOrderSystem>(system)
                .Issue(ref World.Unmanaged.ResolveSystemStateRef(system), unit, order, queue);
        }

        public Entity SpawnUnit(byte faction, float3 position, float speed = 5f, float radius = 0.5f,
            float maxHealth = 100f, string name = "Unit")
        {
            var unit = CreateGameEntity(faction, position, maxHealth, name, out var writer);
            UnitAuthoring.Add(ref writer, speed, new NavAgent { Radius = radius });
            return unit;
        }

        public Entity SpawnBuilding(byte faction, float3 position, float2 footprint, bool complete = true,
            float buildTime = 10f, string name = "Building", int populationProvided = 0, float power = 0f)
        {
            var building = CreateGameEntity(faction, position, 500f, name, out var writer, buildTime);
            BuildingSetup.Add(ref writer, footprint, populationProvided, complete, power);
            return building;
        }

        /// <summary>Turns a spawned entity into a prefab, as baking does for referenced GameObjects.</summary>
        public Entity MakePrefab(Entity entity)
        {
            EntityManager.AddComponent<Prefab>(entity);
            return entity;
        }

        private Entity CreateGameEntity(byte faction, float3 position, float maxHealth, string name,
            out EntityManagerWriter writer, float buildTime = 5f)
        {
            var entity = EntityManager.CreateEntity();
            EntityManager.AddComponentData(entity, LocalTransform.FromPosition(position));
            writer = new EntityManagerWriter(EntityManager, entity);
            GameEntitySetup.Add(ref writer, new GameEntitySpec
            {
                Info = new EntityInfo { TypeId = EntityInfo.TypeIdFromName(name), Name = name },
                Owner = faction,
                MaxHealth = maxHealth,
                VisionRange = 10f,
                EyeHeight = 2f,
                BuildTime = buildTime,
                Population = 1,
                CountsForVictory = true,
            });
            return entity;
        }

        private void CreatePlayer(byte faction, bool local)
        {
            var player = EntityManager.CreateEntity();
            var writer = new EntityManagerWriter(EntityManager, player);
            PlayerSetup.Add(ref writer, faction, $"P{faction}", float4.zero, populationCap: 100);

            if (local)
            {
                EntityManager.AddComponent<LocalPlayer>(player);
            }
        }
    }
}
