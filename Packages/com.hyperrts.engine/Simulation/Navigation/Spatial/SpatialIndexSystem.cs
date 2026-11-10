using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Common.Cells;
using HyperRTS.Simulation.Common.Players;
using HyperRTS.Simulation.Navigation.Agents;
using HyperRTS.Simulation.Navigation.Areas;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Profiling;
using Unity.Transforms;

namespace HyperRTS.Simulation.Navigation.Spatial
{
    /// <summary>
    /// Rebuilds the <see cref="SpatialIndex"/> from every living entity with health and an owner, except passengers
    /// inside a container: snapshots in parallel, then a counting sort by cell.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(MovementSystemGroup), OrderFirst = true)]
    public partial struct SpatialIndexSystem : ISystem
    {
        public const float CellSize = 8f;

        private static readonly ProfilerMarker IndexMarker = new("SpatialIndexSystem.Index");

        private EntityQuery _indexed;

        // One slot per queried entity, empty between frames; the sort reads and empties it.
        private NativeList<SpatialEntry> _snapshots;

        public void OnCreate(ref SystemState state)
        {
            _indexed = SystemAPI.QueryBuilder().WithAll<LocalTransform, Owner, Health>().WithNone<Dead, Inside>().Build();

            _snapshots = new NativeList<SpatialEntry>(1024, Allocator.Persistent);
            // Systems ordered before the first build, such as placement, query it: until then it is one empty cell.
            var starts = new NativeList<int>(1024, Allocator.Persistent);
            starts.Resize(2, NativeArrayOptions.ClearMemory);
            state.EntityManager.CreateSingleton(new SpatialIndex
            {
                Grid = new CellGrid(float2.zero, CellSize, new int2(1)),
                CellStarts = starts,
                Entries = new NativeList<SpatialEntry>(1024, Allocator.Persistent),
                Oversized = new NativeList<SpatialEntry>(16, Allocator.Persistent),
            }, "SpatialIndex");
            state.RequireForUpdate<MapSettings>();
        }

        public void OnDestroy(ref SystemState state)
        {
            state.CompleteDependency();
            if (SystemAPI.TryGetSingletonRW<SpatialIndex>(out var index))
            {
                index.ValueRW.CellStarts.Dispose();
                index.ValueRW.Entries.Dispose();
                index.ValueRW.Oversized.Dispose();
            }

            _snapshots.Dispose();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            ref var index = ref SystemAPI.GetSingletonRW<SpatialIndex>().ValueRW;
            var map = SystemAPI.GetSingleton<MapSettings>();
            index.Grid = new CellGrid(map.Min, CellSize, math.max(1, (int2)math.ceil(map.Size / CellSize)));
            // Unfiltered: an upper bound is enough and doesn't wait on jobs toggling Dead or Inside.
            var count = _indexed.CalculateEntityCountWithoutFiltering();
            if (_snapshots.Length < count)
            {
                _snapshots.Resize(count, NativeArrayOptions.ClearMemory);
            }

            state.Dependency = new SnapshotJob
            {
                Snapshots = _snapshots.AsArray(),
                AgentLookup = SystemAPI.GetComponentLookup<NavAgent>(true),
                ObstacleLookup = SystemAPI.GetComponentLookup<NavObstacle>(true),
                BuildingLookup = SystemAPI.GetComponentLookup<BuildingTag>(true),
                MovingLookup = SystemAPI.GetComponentLookup<MoveDestination>(true),
                ArrivalLookup = SystemAPI.GetComponentLookup<Arrival>(true),
                VelocityLookup = SystemAPI.GetComponentLookup<Velocity>(true),
            }.ScheduleParallel(_indexed, state.Dependency);
            state.Dependency = new SortJob { Index = index, Snapshots = _snapshots.AsArray() }
                .Schedule(state.Dependency);
        }

        [BurstCompile]
        private partial struct SnapshotJob : IJobEntity, IJobEntityChunkBeginEnd
        {
            // Each entity writes only its own slot.
            [NativeDisableParallelForRestriction] public NativeArray<SpatialEntry> Snapshots;
            [ReadOnly] public ComponentLookup<NavAgent> AgentLookup;
            [ReadOnly] public ComponentLookup<NavObstacle> ObstacleLookup;
            [ReadOnly] public ComponentLookup<BuildingTag> BuildingLookup;
            [ReadOnly] public ComponentLookup<MoveDestination> MovingLookup;
            [ReadOnly] public ComponentLookup<Arrival> ArrivalLookup;
            [ReadOnly] public ComponentLookup<Velocity> VelocityLookup;

            // Per chunk, not per unit, so the marker costs little and the profiler stays readable.
            public bool OnChunkBegin(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask,
                in v128 chunkEnabledMask)
            {
                IndexMarker.Begin();
                return true;
            }

            public void OnChunkEnd(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask,
                in v128 chunkEnabledMask, bool chunkWasExecuted) => IndexMarker.End();

            private void Execute([EntityIndexInQuery] int slot, Entity entity, in LocalTransform transform,
                in Owner owner)
            {
                // Without an agent or a destination it is round, unspaced, still, on the ground and in no group.
                var isUnit = AgentLookup.TryGetComponent(entity, out var agent);
                MovingLookup.TryGetComponent(entity, out var destination);
                VelocityLookup.TryGetComponent(entity, out var velocity);
                ArrivalLookup.TryGetComponent(entity, out var arrival);
                var facing = math.normalizesafe(transform.Forward().xz);
                var moving = MovingLookup.HasEnabled(entity);
                var entry = new SpatialEntry
                {
                    Entity = entity,
                    Position = transform.Position,
                    Radius = EntityRadius.Get(entity, AgentLookup, ObstacleLookup),
                    Owner = owner.Value,
                    IsUnit = isUnit,
                    IsMoving = moving,
                    IsArrived = ArrivalLookup.HasEnabled(entity) && arrival.Reason != ArrivalReason.AtGroupmate,
                    Group = destination.Group,
                    Facing = facing,
                    Heading = moving ? math.normalizesafe(destination.Value.xz - transform.Position.xz) : float2.zero,
                    Velocity = velocity.Value,
                    IsBuilding = BuildingLookup.HasComponent(entity),
                    Layer = agent.Layer,
                    SegmentAxis = facing * agent.HalfSegment,
                    Spacing = agent.Spacing,
                    Mass = agent.Mass,
                    IsVehicle = agent.IsVehicle,
                };
                entry.FixReach();
                Snapshots[slot] = entry;
            }
        }
    }
}
