using HyperRTS.Core;
using HyperRTS.Simulation.Buildings;
using HyperRTS.Simulation.Combat;
using HyperRTS.Simulation.Match;
using HyperRTS.Simulation.Navigation;
using HyperRTS.Simulation.Orders;
using HyperRTS.Simulation.Units;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.Resources
{
    /// <summary>Runs Gather orders: walk to the node, fill up, deposit at the nearest owned drop-off, repeat.</summary>
    [BurstCompile]
    [UpdateInGroup(typeof(ProductionSystemGroup))]
    public partial struct GatherSystem : ISystem
    {
        /// <summary>How far from a depleted node harvesters look for another of the same type.</summary>
        public const float ReplacementRange = 30f;

        private EntityQuery _nodes;
        private EntityQuery _dropOffs;
        private EntityQuery _players;
        private ComponentLookup<ResourceNode> _nodeLookup;
        private BufferLookup<ResourceStock> _stockLookup;
        private ComponentLookup<LocalTransform> _transformLookup;
        private ComponentLookup<NavObstacle> _obstacleLookup;
        private ComponentLookup<Faction> _factionLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _nodes = SystemAPI.QueryBuilder().WithAll<ResourceNode, LocalTransform>().Build();
            _dropOffs = SystemAPI.QueryBuilder().WithAll<ResourceDropOff, LocalTransform, Faction>()
                .WithNone<ConstructionProgress, Dead>().Build();
            _players = SystemAPI.QueryBuilder().WithAll<Player>().Build();
            _nodeLookup = state.GetComponentLookup<ResourceNode>();
            _stockLookup = state.GetBufferLookup<ResourceStock>();
            _transformLookup = state.GetComponentLookup<LocalTransform>(true);
            _obstacleLookup = state.GetComponentLookup<NavObstacle>(true);
            _factionLookup = state.GetComponentLookup<Faction>(true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var allocator = state.WorldUpdateAllocator;
            var nodes = _nodes.ToEntityListAsync(allocator, state.Dependency, out var nodesReady);
            var dropOffs = _dropOffs.ToEntityListAsync(allocator, state.Dependency, out var dropOffsReady);
            state.Dependency = JobHandle.CombineDependencies(nodesReady, dropOffsReady);

            _nodeLookup.Update(ref state);
            _stockLookup.Update(ref state);
            _transformLookup.Update(ref state);
            _obstacleLookup.Update(ref state);
            _factionLookup.Update(ref state);

            new GatherJob
            {
                DeltaTime = SystemAPI.Time.DeltaTime,
                Nodes = nodes.AsDeferredJobArray(),
                DropOffs = dropOffs.AsDeferredJobArray(),
                PlayerByFaction = PlayerLookup.ByFaction(_players, allocator),
                NodeLookup = _nodeLookup,
                StockLookup = _stockLookup,
                TransformLookup = _transformLookup,
                ObstacleLookup = _obstacleLookup,
                FactionLookup = _factionLookup,
            }.Schedule();
        }

        /// <summary>Single-threaded: harvesters drain shared nodes and stockpiles.</summary>
        [BurstCompile]
        [WithNone(typeof(Dead))]
        [WithPresent(typeof(MoveDestination))]
        private partial struct GatherJob : IJobEntity
        {
            public float DeltaTime;
            [ReadOnly] public NativeArray<Entity> Nodes;
            [ReadOnly] public NativeArray<Entity> DropOffs;
            [ReadOnly] public NativeArray<Entity> PlayerByFaction;
            public ComponentLookup<ResourceNode> NodeLookup;
            public BufferLookup<ResourceStock> StockLookup;
            [ReadOnly] public ComponentLookup<LocalTransform> TransformLookup;
            [ReadOnly] public ComponentLookup<NavObstacle> ObstacleLookup;
            [ReadOnly] public ComponentLookup<Faction> FactionLookup;

            private void Execute(ref Harvester harvester, ref HarvestState harvest, ref ActiveOrder order,
                EnabledRefRW<ActiveOrder> busy, ref MoveDestination destination, EnabledRefRW<MoveDestination> moving,
                in LocalTransform transform, in NavAgent agent, in Faction faction)
            {
                if (!busy.ValueRO || order.Value.Type != OrderType.Gather)
                {
                    return;
                }

                if (harvest.Phase == HarvestPhase.Returning)
                {
                    Return(ref harvester, ref harvest, ref destination, moving, transform.Position, agent.Radius,
                        faction.Value);
                    return;
                }

                if (!IsHarvestable(order.Value.Target) && !TryReplaceNode(ref order, harvest))
                {
                    // Nothing left nearby: deliver what is carried, then the order is done.
                    harvest.Phase = HarvestPhase.Returning;
                    if (harvester.CargoAmount <= 0)
                    {
                        busy.ValueRW = false;
                        moving.ValueRW = false;
                    }

                    return;
                }

                var node = order.Value.Target;
                var nodePosition = TransformLookup[node].Position;
                var extents = ReachMath.HalfExtents(ObstacleLookup, node);
                if (!ReachMath.InReach(transform.Position, agent.Radius, nodePosition, extents))
                {
                    ReachMath.MoveTo(ref destination, moving, nodePosition);
                    return;
                }

                moving.ValueRW = false;
                Gather(ref harvester, ref harvest, node, nodePosition);
            }

            private void Gather(ref Harvester harvester, ref HarvestState harvest, Entity entity, float3 position)
            {
                ref var node = ref NodeLookup.GetRefRW(entity).ValueRW;
                if (harvester.CargoAmount == 0 || !harvester.CargoType.Equals(node.Type))
                {
                    harvester.CargoType = node.Type;
                    harvester.CargoAmount = 0;
                }

                harvest.NodePosition = position;
                harvest.NodeType = node.Type;
                harvest.Progress += harvester.GatherRate * DeltaTime;

                var whole = (int)harvest.Progress;
                harvest.Progress -= whole;
                var taken = math.min(whole, math.min(node.Amount, harvester.Capacity - harvester.CargoAmount));
                node.Amount -= taken;
                harvester.CargoAmount += taken;

                if (harvester.CargoAmount >= harvester.Capacity)
                {
                    harvest.Phase = HarvestPhase.Returning;
                    harvest.Progress = 0f;
                }
            }

            private void Return(ref Harvester harvester, ref HarvestState harvest, ref MoveDestination destination,
                EnabledRefRW<MoveDestination> moving, float3 position, float radius, byte faction)
            {
                var dropOff = NearestDropOff(position, faction);
                if (harvester.CargoAmount <= 0 || dropOff == Entity.Null)
                {
                    // Empty-handed goes straight back to work; with no drop-off, wait until one is built.
                    harvest.Phase = harvester.CargoAmount <= 0 ? HarvestPhase.Gathering : harvest.Phase;
                    moving.ValueRW = false;
                    return;
                }

                var target = TransformLookup[dropOff].Position;
                if (!ReachMath.InReach(position, radius, target, ReachMath.HalfExtents(ObstacleLookup, dropOff)))
                {
                    ReachMath.MoveTo(ref destination, moving, target);
                    return;
                }

                moving.ValueRW = false;
                if (StockLookup.TryGetBuffer(PlayerByFaction[faction], out var stock))
                {
                    ResourceMath.Add(stock, harvester.CargoType, harvester.CargoAmount);
                }

                harvester.CargoAmount = 0;
                harvest.Phase = HarvestPhase.Gathering;
            }

            private bool IsHarvestable(Entity node) =>
                NodeLookup.TryGetComponent(node, out var data) && data.Amount > 0 && TransformLookup.HasComponent(node);

            private bool TryReplaceNode(ref ActiveOrder order, in HarvestState harvest)
            {
                var best = Entity.Null;
                var bestDistance = ReplacementRange * ReplacementRange;
                foreach (var candidate in Nodes)
                {
                    var node = NodeLookup[candidate];
                    var distance = math.distancesq(TransformLookup[candidate].Position.xz, harvest.NodePosition.xz);
                    if (node.Amount > 0 && node.Type.Equals(harvest.NodeType) && distance <= bestDistance)
                    {
                        best = candidate;
                        bestDistance = distance;
                    }
                }

                order.Value.Target = best;
                return best != Entity.Null;
            }

            private Entity NearestDropOff(float3 position, byte faction)
            {
                var best = Entity.Null;
                var bestDistance = float.MaxValue;
                foreach (var candidate in DropOffs)
                {
                    var distance = math.distancesq(TransformLookup[candidate].Position.xz, position.xz);
                    if (FactionLookup[candidate].Value == faction && distance < bestDistance)
                    {
                        best = candidate;
                        bestDistance = distance;
                    }
                }

                return best;
            }
        }
    }
}
