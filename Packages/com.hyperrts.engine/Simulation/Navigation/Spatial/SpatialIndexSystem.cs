using HyperRTS.Core;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Navigation.Agents;
using HyperRTS.Simulation.Navigation.Areas;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Profiling;
using Unity.Transforms;

namespace HyperRTS.Simulation.Navigation.Spatial
{
    /// <summary>
    /// Rebuilds the <see cref="SpatialIndex"/> from every living entity with health and an owner, except passengers
    /// inside a container.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(MovementSystemGroup), OrderFirst = true)]
    public partial struct SpatialIndexSystem : ISystem
    {
        public const float CellSize = 8f;

        private static readonly ProfilerMarker IndexMarker = new("SpatialIndexSystem.Index");

        private EntityQuery _indexed;

        public void OnCreate(ref SystemState state)
        {
            _indexed = SystemAPI.QueryBuilder().WithAll<LocalTransform, Faction, Health>().WithNone<Dead, Inside>().Build();

            state.EntityManager.CreateSingleton(new SpatialIndex
            {
                Cells = new NativeParallelMultiHashMap<int, SpatialEntry>(1024, Allocator.Persistent),
                CellSize = CellSize,
                Oversized = new NativeList<SpatialEntry>(16, Allocator.Persistent),
            }, "SpatialIndex");
        }

        public void OnDestroy(ref SystemState state)
        {
            state.CompleteDependency();
            if (SystemAPI.TryGetSingletonRW<SpatialIndex>(out var index))
            {
                index.ValueRW.Cells.Dispose();
                index.ValueRW.Oversized.Dispose();
            }
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            ref var index = ref SystemAPI.GetSingletonRW<SpatialIndex>().ValueRW;
            // Unfiltered: an upper bound is enough for capacity and doesn't wait on jobs toggling Dead or Inside.
            var count = _indexed.CalculateEntityCountWithoutFiltering();
            index.Cells.Clear();
            index.Oversized.Clear();
            if (index.Cells.Capacity < count)
            {
                index.Cells.Capacity = count;
            }

            // The parallel writer can't grow the list, so size it for the worst case.
            if (index.Oversized.Capacity < count)
            {
                index.Oversized.Capacity = count;
            }


            state.Dependency = new IndexJob
            {
                Writer = index.Cells.AsParallelWriter(),
                Oversized = index.Oversized.AsParallelWriter(),
                CellSize = index.CellSize,
                AgentLookup = SystemAPI.GetComponentLookup<NavAgent>(true),
                ObstacleLookup = SystemAPI.GetComponentLookup<NavObstacle>(true),
                BuildingLookup = SystemAPI.GetComponentLookup<BuildingTag>(true),
                MovingLookup = SystemAPI.GetComponentLookup<MoveDestination>(true),
                ArrivalLookup = SystemAPI.GetComponentLookup<Arrival>(true),
                VelocityLookup = SystemAPI.GetComponentLookup<Velocity>(true),
            }.ScheduleParallel(_indexed, state.Dependency);
        }

        [BurstCompile]
        private partial struct IndexJob : IJobEntity, IJobEntityChunkBeginEnd
        {
            public NativeParallelMultiHashMap<int, SpatialEntry>.ParallelWriter Writer;
            public NativeList<SpatialEntry>.ParallelWriter Oversized;
            public float CellSize;
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

            private void Execute(Entity entity, in LocalTransform transform, in Faction faction)
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
                    Faction = faction.Value,
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
                };
                entry.AddTo(Writer, Oversized, CellSize);
            }
        }
    }
}
