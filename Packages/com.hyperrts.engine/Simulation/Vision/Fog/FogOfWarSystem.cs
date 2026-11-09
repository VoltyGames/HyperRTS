using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Common.Players;
using HyperRTS.Simulation.Navigation.Agents;
using HyperRTS.Simulation.Navigation.Heights;
using HyperRTS.Simulation.Vision.Sight;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Transforms;

namespace HyperRTS.Simulation.Vision.Fog
{
    /// <summary>
    /// Creates the <see cref="FogOfWar"/> grid from <see cref="MapSettings"/> (again when the map's area changes) and
    /// restamps it a few times per second: changed <see cref="SightArea"/>s redraw in parallel, team planes rebuild and
    /// detection is stamped. With fog disabled the grid stays fully visible.
    /// </summary>
    [BurstCompile]
    [WorldSystemFilter(SimulationWorlds.All)]
    [UpdateInGroup(typeof(CombatSystemGroup), OrderFirst = true)]
    public partial struct FogOfWarSystem : ISystem
    {
        /// <summary>Seconds between restamps; vision needn't track movement every frame.</summary>
        public const float UpdateInterval = 0.1f;

        private EntityQuery _viewers;
        private double _nextUpdate;
        private bool _stampedWithFog;
        private MapSettings _builtMap;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _viewers = SystemAPI.QueryBuilder().WithAll<SightArea, SightAreaBits, VisionRange, Owner, LocalTransform>()
                .Build();
            state.RequireForUpdate<MapSettings>();
            state.RequireForUpdate<OwnerRelations>();
            state.RequireForUpdate<SightBlockers>();
        }

        public void OnDestroy(ref SystemState state)
        {
            state.CompleteDependency();
            if (SystemAPI.TryGetSingleton<FogOfWar>(out var fog))
            {
                fog.Dispose();
            }
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var settings = SystemAPI.GetSingleton<MapSettings>();
            var rebuilt = EnsureGrid(ref state, settings);

            // Toggling fog restamps at once, so nothing reads a grid stamped under the old setting.
            var elapsed = SystemAPI.Time.ElapsedTime;
            if (!rebuilt && elapsed < _nextUpdate && settings.FogOfWar == _stampedWithFog)
            {
                return;
            }

            _nextUpdate = elapsed + UpdateInterval;
            _stampedWithFog = settings.FogOfWar;
            ref var fog = ref SystemAPI.GetSingletonRW<FogOfWar>().ValueRW;
            var relations = SystemAPI.GetSingleton<OwnerRelations>();
            fog.Version++;

            // Stealth and concealment apply with fog off too, so detection and the close layer are stamped either way.
            state.Dependency = ClearJob<ulong>.Schedule(fog.Detected, state.Dependency);
            new DetectionStampJob { Fog = fog, Relations = relations }.Schedule();
            DrawSightAreas(ref state, fog, relations, settings);
            Merge(ref state, fog);
            state.Dependency = settings.FogOfWar
                ? new ExploreJob { VisibleTall = fog.VisibleTall, Explored = fog.Explored }
                    .Schedule(fog.Explored.Length, 4096, state.Dependency)
                : new RevealAllJob { Visible = fog.Visible, VisibleTall = fog.VisibleTall, Explored = fog.Explored }
                    .Schedule(fog.Visible.Length, 4096, state.Dependency);
        }

        private void DrawSightAreas(ref SystemState state, in FogOfWar fog, in OwnerRelations relations,
            in MapSettings settings)
        {
            SystemAPI.TryGetSingleton<TerrainHeight>(out var terrain);
            SystemAPI.TryGetSingleton<HighGround>(out var highGround);
            SystemAPI.TryGetSingleton<SightRules>(out var rules);
            var blockers = SystemAPI.GetSingletonRW<SightBlockers>().ValueRO;
            state.Dependency = new SightAreaJob
            {
                Fog = fog,
                Relations = relations,
                Terrain = terrain,
                Blockers = blockers,
                HighGround = highGround,
                WaterFloor = settings.FloodTerrain ? settings.WaterLevel : float.NegativeInfinity,
                RevealDistance = rules.RevealDistance,
                Inside = SystemAPI.GetComponentLookup<Inside>(true),
                Blocks = SystemAPI.GetComponentLookup<SightBlock>(true),
                Eyes = SystemAPI.GetComponentLookup<PassengerEye>(true),
                Agents = SystemAPI.GetComponentLookup<NavAgent>(true),
                Transforms = SystemAPI.GetComponentLookup<LocalTransform>(true),
            }.ScheduleParallel(_viewers, state.Dependency);

            // Every area has now drawn round what changed.
            state.Dependency = ClearJob<byte>.Schedule(blockers.DirtyTiles, state.Dependency);
        }

        private void Merge(ref SystemState state, in FogOfWar fog)
        {
            var viewers = _viewers.ToEntityListAsync(Allocator.TempJob, state.Dependency, out var listed);
            state.Dependency = new FogMergeJob
            {
                Fog = fog,
                Viewers = viewers.AsDeferredJobArray(),
                Areas = SystemAPI.GetComponentLookup<SightArea>(true),
                Bits = SystemAPI.GetBufferLookup<SightAreaBits>(true),
            }.Schedule(FogMergeJob.Bands(fog), 1, listed);
            viewers.Dispose(state.Dependency);
        }

        /// <summary>Creates the grid, or replaces it when the map's area or fog cell size changed; true if it did.</summary>
        private bool EnsureGrid(ref SystemState state, in MapSettings settings)
        {
            // Read-only check: write access would complete every job reading the fog, every frame.
            var exists = SystemAPI.HasSingleton<FogOfWar>();
            if (exists && settings.SameFogGrid(_builtMap))
            {
                return false;
            }

            _builtMap = settings;
            var grid = FogOfWar.Create(settings.FogGrid(), Allocator.Persistent);
            grid.Generation = 1;
            state.ReplaceSingleton(ref grid, "FogOfWar");
            return true;
        }

        [BurstCompile]
        private struct RevealAllJob : IJobParallelFor
        {
            public NativeArray<ulong> Visible;
            public NativeArray<ulong> VisibleTall;
            public NativeArray<ulong> Explored;

            public void Execute(int index)
            {
                Visible[index] = ulong.MaxValue;
                VisibleTall[index] = ulong.MaxValue;
                Explored[index] = ulong.MaxValue;
            }
        }

        [BurstCompile]
        private struct ExploreJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<ulong> VisibleTall;
            public NativeArray<ulong> Explored;

            public void Execute(int index) => Explored[index] |= VisibleTall[index];
        }
    }
}
