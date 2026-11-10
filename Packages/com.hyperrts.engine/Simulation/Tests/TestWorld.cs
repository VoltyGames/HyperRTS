using HyperRTS.Simulation.Common.Authoring;
using HyperRTS.Simulation.Common.Players;
using HyperRTS.Simulation.Common.Prefabs;
using HyperRTS.Simulation.Production;
using HyperRTS.Simulation.Squads.Cover;
using HyperRTS.Simulation.Vision.Fog;
using HyperRTS.Simulation.Vision.Sight;
using System;
using System.Collections.Generic;
using HyperRTS.Simulation.AI;
using HyperRTS.Simulation.Buildings;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Cues.Voice;
using HyperRTS.Simulation.GameEntities;
using HyperRTS.Simulation.Match;
using HyperRTS.Simulation.MatchStart;
using HyperRTS.Simulation.Navigation.Agents;
using HyperRTS.Simulation.Navigation.Areas;
using HyperRTS.Simulation.Navigation.Grid;
using HyperRTS.Simulation.Navigation.Heights;
using HyperRTS.Simulation.Orders;
using HyperRTS.Simulation.Squads;
using HyperRTS.Simulation.Vision;
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
    public sealed partial class TestWorld : IDisposable
    {
        public const float FrameTime = 1f / 30f;

        public readonly World World;
        private readonly List<BlobAssetReference<HeightfieldBlob>> _terrains = new();
        private readonly List<UnityEngine.Object> _owned = new();
        private readonly List<Action> _checks = new();
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
                // The command buffer systems order themselves against these groups.
                typeof(FixedStepSimulationSystemGroup),
                typeof(VariableRateSimulationSystemGroup),
                typeof(LateSimulationSystemGroup),
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
                foreach (var check in _checks)
                {
                    check();
                }
            }
        }

        /// <summary>Runs <paramref name="check"/> after every tick from now on, for rules that must hold every frame.</summary>
        public void CheckEveryTick(Action check) => _checks.Add(check);

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

        /// <summary>
        /// Creates map, rules, relations, AI tunings, one player per team entry (owner = index + 1, first is local)
        /// and the nav grid, plus AI map hints that <see cref="AIHintsTestKit.CreateHints"/> replaces.
        /// </summary>
        public void CreateMatch(params byte[] teams) => CreateMatch(teams, 200f);

        /// <summary><see cref="CreateMatch(byte[])"/> on a square map <paramref name="size"/> metres across, centred on 0.</summary>
        public void CreateMatch(byte[] teams, float size)
        {
            var match = EntityManager.CreateEntity();
            EntityManager.AddComponentData(match, new MapSettings
            {
                Min = new float2(size * -0.5f),
                Size = new float2(size),
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
            EntityManager.AddComponentData(match, SightSettings.Standard.Rules);
            EntityManager.AddComponentData(match, CoverSettings.Standard.Rules);
            EntityManager.AddComponentData(match, SquadSettings.Standard.Rules);
            EntityManager.AddComponentData(match, VoiceSettings.Standard.Rules);

            var relations = new OwnerRelations();
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
            var writer = new EntityWriter(EntityManager, EntityManager.CreateEntity());
            NavSetup.AddTerrain(writer, terrain);
            BuildNavGrid();
        }

        /// <summary>Builds the nav grid over the current map and terrain, as the map's first frames do.</summary>
        private void BuildNavGrid()
        {
            World.GetExistingSystem<NavGridSystem>().Update(World.Unmanaged);
            EntityManager.CompleteAllTrackedJobs();
        }
    }
}
