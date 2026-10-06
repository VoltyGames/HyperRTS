using HyperRTS.Core;
using HyperRTS.Simulation.Buildings;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Navigation;
using HyperRTS.Simulation.Transport;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;

namespace HyperRTS.Simulation.Spatial
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

        private EntityQuery _indexed;
        private ComponentLookup<NavAgent> _agentLookup;
        private ComponentLookup<NavObstacle> _obstacleLookup;
        private ComponentLookup<BuildingTag> _buildingLookup;

        public void OnCreate(ref SystemState state)
        {
            _indexed = SystemAPI.QueryBuilder().WithAll<LocalTransform, Faction, Health>().WithNone<Dead, Inside>().Build();
            _agentLookup = state.GetComponentLookup<NavAgent>(true);
            _obstacleLookup = state.GetComponentLookup<NavObstacle>(true);
            _buildingLookup = state.GetComponentLookup<BuildingTag>(true);

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
            var count = _indexed.CalculateEntityCount();
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

            new IndexJob
            {
                Writer = index.Cells.AsParallelWriter(),
                Oversized = index.Oversized.AsParallelWriter(),
                CellSize = index.CellSize,
                AgentLookup = _agentLookup,
                ObstacleLookup = _obstacleLookup,
                BuildingLookup = _buildingLookup,
            }.ScheduleParallel(_indexed);
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
                var entry = new SpatialEntry
                {
                    Entity = entity,
                    Position = transform.Position,
                    Radius = EntityRadius.Of(entity, AgentLookup, ObstacleLookup),
                    Faction = faction.Value,
                    IsUnit = !BuildingLookup.HasComponent(entity),
                    Layer = NavAgent.LayerOf(AgentLookup, entity),
                };

                if (entry.Radius > CellSize)
                {
                    Oversized.AddNoResize(entry);
                    return;
                }

                Writer.Add(SpatialIndex.Key(SpatialIndex.CellOf(transform.Position, CellSize)), entry);
            }
        }
    }
}
