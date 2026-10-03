using System;
using System.Collections.Generic;
using HyperRTS.Core;
using HyperRTS.Simulation.Buildings;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Match;
using HyperRTS.Simulation.Orders;
using HyperRTS.Simulation.Resources;
using HyperRTS.Simulation.Units;
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
        private double _elapsed;

        public TestWorld()
        {
            World = new World("HyperRTS Test World");

            var simulation = typeof(UnitTag).Assembly;
            var core = typeof(OrderSystemGroup).Assembly;
            var systems = new List<Type>
            {
                typeof(SimulationSystemGroup),
                typeof(TransformSystemGroup),
                typeof(BeginSimulationEntityCommandBufferSystem),
                typeof(EndSimulationEntityCommandBufferSystem),
            };

            foreach (var type in DefaultWorldInitialization.GetAllSystems(WorldSystemFilterFlags.Default))
            {
                if (type.Assembly == simulation || type.Assembly == core)
                {
                    systems.Add(type);
                }
            }

            DefaultWorldInitialization.AddSystemsToRootLevelSystemGroups(World, systems);
        }

        public EntityManager EntityManager => World.EntityManager;

        public void Dispose()
        {
            if (World.IsCreated)
            {
                World.Dispose();
            }
        }

        /// <summary>Updates only the simulation group so the pushed time isn't overwritten.</summary>
        public void Tick(float deltaTime = FrameTime, int frames = 1)
        {
            for (var i = 0; i < frames; i++)
            {
                _elapsed += deltaTime;
                World.SetTime(new TimeData(_elapsed, deltaTime));
                World.GetExistingSystemManaged<SimulationSystemGroup>().Update();
            }
        }

        /// <summary>Ticks fixed frames until <paramref name="seconds"/> of game time have passed.</summary>
        public void Run(float seconds) => Tick(FrameTime, (int)math.ceil(seconds / FrameTime));

        public T Get<T>(Entity entity) where T : unmanaged, IComponentData => EntityManager.GetComponentData<T>(entity);

        public bool IsEnabled<T>(Entity entity) where T : unmanaged, IComponentData, IEnableableComponent =>
            EntityManager.IsComponentEnabled<T>(entity);

        /// <summary>Creates map, relations and one player per team entry (faction = index + 1, first is local).</summary>
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

            var relations = new FactionRelations();
            relations.Teams.Add(0);
            for (var i = 0; i < teams.Length; i++)
            {
                relations.Teams.Add(teams[i]);
                CreatePlayer((byte)(i + 1), teams[i], local: i == 0);
            }

            EntityManager.AddComponentData(match, relations);
        }

        public Entity Player(byte faction)
        {
            using var query = EntityManager.CreateEntityQuery(typeof(Player));
            using var players = query.ToEntityArray(Unity.Collections.Allocator.Temp);
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

        public Entity SpawnUnit(byte faction, float3 position, float speed = 5f, float radius = 0.5f,
            float maxHealth = 100f, string name = "Unit")
        {
            var unit = CreateGameEntity(faction, position, maxHealth, name, out var sink);
            UnitSetup.Add(ref sink, speed, radius);
            return unit;
        }

        public Entity SpawnBuilding(byte faction, float3 position, float2 footprint, bool complete = true,
            float buildTime = 10f, string name = "Building")
        {
            var building = CreateGameEntity(faction, position, 500f, name, out var sink, buildTime);
            BuildingSetup.Add(ref sink, footprint, 0, complete);
            return building;
        }

        /// <summary>Turns a spawned entity into a prefab, as baking does for referenced GameObjects.</summary>
        public Entity MakePrefab(Entity entity)
        {
            EntityManager.AddComponent<Prefab>(entity);
            return entity;
        }

        private Entity CreateGameEntity(byte faction, float3 position, float maxHealth, string name,
            out EntityManagerSink sink, float buildTime = 5f)
        {
            var entity = EntityManager.CreateEntity();
            EntityManager.AddComponentData(entity, LocalTransform.FromPosition(position));
            sink = new EntityManagerSink(EntityManager, entity);
            GameEntitySetup.Add(ref sink, new GameEntitySpec
            {
                TypeId = EntityInfo.TypeIdFromName(name),
                Name = name,
                Owner = faction,
                MaxHealth = maxHealth,
                VisionRange = 10f,
                BuildTime = buildTime,
                Population = 1,
                CountsForVictory = true,
            });
            return entity;
        }

        private void CreatePlayer(byte faction, byte team, bool local)
        {
            var player = EntityManager.CreateEntity();
            EntityManager.AddComponentData(player, new Player { Faction = faction, Team = team, Name = $"P{faction}" });
            EntityManager.AddComponentData(player, new Population { Cap = 100 });
            EntityManager.AddComponent<Defeated>(player);
            EntityManager.SetComponentEnabled<Defeated>(player, false);
            EntityManager.AddBuffer<PlayerCommand>(player);
            EntityManager.AddBuffer<ResourceStock>(player);

            if (local)
            {
                EntityManager.AddComponent<LocalPlayer>(player);
            }
        }
    }
}
