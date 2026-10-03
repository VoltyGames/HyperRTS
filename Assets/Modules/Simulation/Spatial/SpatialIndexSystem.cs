using HyperRTS.Core;
using HyperRTS.Simulation.Buildings;
using HyperRTS.Simulation.Combat;
using HyperRTS.Simulation.Match;
using HyperRTS.Simulation.Navigation;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.Spatial
{
    /// <summary>Rebuilds the <see cref="SpatialIndex"/> from every living entity with health and an owner.</summary>
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
            _indexed = SystemAPI.QueryBuilder().WithAll<LocalTransform, Faction, Health>().WithNone<Dead>().Build();
            _agentLookup = state.GetComponentLookup<NavAgent>(true);
            _obstacleLookup = state.GetComponentLookup<NavObstacle>(true);
            _buildingLookup = state.GetComponentLookup<BuildingTag>(true);

            state.EntityManager.CreateSingleton(new SpatialIndex
            {
                Cells = new NativeParallelMultiHashMap<int, SpatialEntry>(1024, Allocator.Persistent),
                CellSize = CellSize,
            }, "SpatialIndex");
        }

        public void OnDestroy(ref SystemState state)
        {
            state.CompleteDependency();
            if (SystemAPI.TryGetSingletonRW<SpatialIndex>(out var index))
            {
                index.ValueRW.Cells.Dispose();
            }
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            ref var index = ref SystemAPI.GetSingletonRW<SpatialIndex>().ValueRW;
            var count = _indexed.CalculateEntityCount();
            index.Cells.Clear();
            if (index.Cells.Capacity < count)
            {
                index.Cells.Capacity = count;
            }

            _agentLookup.Update(ref state);
            _obstacleLookup.Update(ref state);
            _buildingLookup.Update(ref state);

            new IndexJob
            {
                Writer = index.Cells.AsParallelWriter(),
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
            public float CellSize;
            [ReadOnly] public ComponentLookup<NavAgent> AgentLookup;
            [ReadOnly] public ComponentLookup<NavObstacle> ObstacleLookup;
            [ReadOnly] public ComponentLookup<BuildingTag> BuildingLookup;

            private void Execute(Entity entity, in LocalTransform transform, in Faction faction)
            {
                var radius = 0.5f;
                if (AgentLookup.TryGetComponent(entity, out var agent))
                {
                    radius = agent.Radius;
                }
                else if (ObstacleLookup.TryGetComponent(entity, out var obstacle))
                {
                    radius = math.cmax(obstacle.Size) * 0.5f;
                }

                var cell = (int2)math.floor(transform.Position.xz / CellSize);
                Writer.Add(SpatialIndex.Key(cell), new SpatialEntry
                {
                    Entity = entity,
                    Position = transform.Position,
                    Radius = radius,
                    Faction = faction.Value,
                    Flags = BuildingLookup.HasComponent(entity) ? SpatialFlags.Building : SpatialFlags.Unit,
                });
            }
        }
    }
}
