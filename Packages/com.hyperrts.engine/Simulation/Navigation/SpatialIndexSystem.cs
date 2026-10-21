using HyperRTS.Core;
using HyperRTS.Simulation.Common;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Transforms;

namespace HyperRTS.Simulation.Navigation
{
    /// <summary>
    /// Rebuilds the <see cref="SpatialIndex"/> from every living entity with health and an owner, except passengers
    /// inside a container, and its blocker grid from every enabled <see cref="Blocker"/>.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(MovementSystemGroup), OrderFirst = true)]
    public partial struct SpatialIndexSystem : ISystem
    {
        public const float CellSize = 8f;

        private EntityQuery _indexed;
        private EntityQuery _blockers;
        private ComponentLookup<NavAgent> _agentLookup;
        private ComponentLookup<NavObstacle> _obstacleLookup;
        private ComponentLookup<BuildingTag> _buildingLookup;

        public void OnCreate(ref SystemState state)
        {
            _indexed = SystemAPI.QueryBuilder().WithAll<LocalTransform, Faction, Health>().WithNone<Dead, Inside>().Build();
            _blockers = SystemAPI.QueryBuilder().WithAll<LocalTransform, Blocker>().Build();
            _agentLookup = state.GetComponentLookup<NavAgent>(true);
            _obstacleLookup = state.GetComponentLookup<NavObstacle>(true);
            _buildingLookup = state.GetComponentLookup<BuildingTag>(true);

            state.EntityManager.CreateSingleton(new SpatialIndex
            {
                Cells = new NativeParallelMultiHashMap<int, SpatialEntry>(1024, Allocator.Persistent),
                CellSize = CellSize,
                Oversized = new NativeList<SpatialEntry>(16, Allocator.Persistent),
                Blockers = new NativeParallelMultiHashMap<int, SpatialEntry>(64, Allocator.Persistent),
                WideBlockers = new NativeList<SpatialEntry>(4, Allocator.Persistent),
            }, "SpatialIndex");
        }

        public void OnDestroy(ref SystemState state)
        {
            state.CompleteDependency();
            if (SystemAPI.TryGetSingletonRW<SpatialIndex>(out var index))
            {
                index.ValueRW.Cells.Dispose();
                index.ValueRW.Oversized.Dispose();
                index.ValueRW.Blockers.Dispose();
                index.ValueRW.WideBlockers.Dispose();
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

            _agentLookup.Update(ref state);
            _obstacleLookup.Update(ref state);
            _buildingLookup.Update(ref state);

            var input = state.Dependency;
            var indexed = new IndexJob
            {
                Writer = index.Cells.AsParallelWriter(),
                Oversized = index.Oversized.AsParallelWriter(),
                CellSize = index.CellSize,
                AgentLookup = _agentLookup,
                ObstacleLookup = _obstacleLookup,
                BuildingLookup = _buildingLookup,
            }.ScheduleParallel(_indexed, input);
            state.Dependency = JobHandle.CombineDependencies(indexed, IndexBlockers(ref state, ref index, input));
        }

        // Writes only the blocker containers, so it runs alongside IndexJob.
        private JobHandle IndexBlockers(ref SystemState state, ref SpatialIndex index, JobHandle input)
        {
            var count = _blockers.CalculateEntityCountWithoutFiltering();
            index.Blockers.Clear();
            index.WideBlockers.Clear();
            if (index.Blockers.Capacity < count)
            {
                index.Blockers.Capacity = count;
            }

            if (index.WideBlockers.Capacity < count)
            {
                index.WideBlockers.Capacity = count;
            }

            return new BlockerJob
            {
                Writer = index.Blockers.AsParallelWriter(),
                Wide = index.WideBlockers.AsParallelWriter(),
                CellSize = index.CellSize,
            }.ScheduleParallel(_blockers, input);
        }

        [BurstCompile]
        private partial struct IndexJob : IJobEntity
        {
            public NativeParallelMultiHashMap<int, SpatialEntry>.ParallelWriter Writer;
            public NativeList<SpatialEntry>.ParallelWriter Oversized;
            public float CellSize;
            [ReadOnly] public ComponentLookup<NavAgent> AgentLookup;
            [ReadOnly] public ComponentLookup<NavObstacle> ObstacleLookup;
            [ReadOnly] public ComponentLookup<BuildingTag> BuildingLookup;

            private void Execute(Entity entity, in LocalTransform transform, in Faction faction)
            {
                // Without an agent the default is round, unspaced and on the ground.
                var isAgent = AgentLookup.TryGetComponent(entity, out var agent);
                var entry = new SpatialEntry
                {
                    Entity = entity,
                    Position = transform.Position,
                    Radius = EntityRadius.Get(entity, AgentLookup, ObstacleLookup),
                    Faction = faction.Value,
                    IsUnit = !BuildingLookup.HasComponent(entity),
                    IsAgent = isAgent,
                    Layer = agent.Layer,
                    SegmentAxis = agent.SegmentAxis(transform.Rotation),
                    Spacing = agent.Spacing,
                };
                entry.AddTo(Writer, Oversized, CellSize);
            }
        }

        [BurstCompile]
        private partial struct BlockerJob : IJobEntity
        {
            public NativeParallelMultiHashMap<int, SpatialEntry>.ParallelWriter Writer;
            public NativeList<SpatialEntry>.ParallelWriter Wide;
            public float CellSize;

            private void Execute(Entity entity, in LocalTransform transform, in Blocker blocker)
            {
                var entry = new SpatialEntry
                {
                    Entity = entity,
                    Position = transform.Position,
                    Radius = blocker.Radius,
                    IsBlocker = true,
                    Layer = NavLayer.Ground,
                    SegmentAxis = blocker.SegmentAxis(transform.Rotation),
                };
                entry.AddTo(Writer, Wide, CellSize);
            }
        }
    }
}
