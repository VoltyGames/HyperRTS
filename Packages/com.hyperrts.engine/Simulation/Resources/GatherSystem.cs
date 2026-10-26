using HyperRTS.Core;
using HyperRTS.Simulation.Common;
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
    /// <summary>
    /// Runs Gather orders: walk to the node, fill up, deposit at an owned drop-off chosen once per trip by its edge
    /// distance and the turn needed to face it, repeat.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(ProductionSystemGroup))]
    public partial struct GatherSystem : ISystem
    {
        private EntityQuery _nodes;
        private EntityQuery _dropOffs;
        private ComponentLookup<ResourceNode> _nodeLookup;
        private BufferLookup<ResourceStock> _stockLookup;
        private TargetReach _reach;
        private ComponentLookup<Faction> _factionLookup;
        private BufferLookup<StatModifier> _modifierLookup;
        private ComponentLookup<PlayerStats> _statsLookup;
        private ComponentLookup<Locomotion> _locomotionLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _nodes = SystemAPI.QueryBuilder().WithAll<ResourceNode, LocalTransform>().Build();
            _dropOffs = SystemAPI.QueryBuilder().WithAll<ResourceDropOff, LocalTransform, Faction>()
                .WithNone<ConstructionProgress, Dead>().Build();
            state.RequireForUpdate<PlayerRegistry>();
            _nodeLookup = state.GetComponentLookup<ResourceNode>();
            _stockLookup = state.GetBufferLookup<ResourceStock>();
            _reach = new TargetReach(ref state);
            state.RequireForUpdate<MapSettings>();
            _factionLookup = state.GetComponentLookup<Faction>(true);
            _modifierLookup = state.GetBufferLookup<StatModifier>(true);
            _statsLookup = state.GetComponentLookup<PlayerStats>();
            _locomotionLookup = state.GetComponentLookup<Locomotion>(true);
            state.RequireForUpdate<NavGrid>();
            state.RequireForUpdate(SystemAPI.QueryBuilder().WithAll<Harvester, ActiveOrder>().Build());
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
            _reach.Update(ref state, SystemAPI.GetSingleton<MapSettings>());
            _factionLookup.Update(ref state);
            _modifierLookup.Update(ref state);
            _statsLookup.Update(ref state);
            _locomotionLookup.Update(ref state);

            new GatherJob
            {
                DeltaTime = SystemAPI.Time.DeltaTime,
                Nodes = nodes.AsDeferredJobArray(),
                DropOffs = dropOffs.AsDeferredJobArray(),
                Players = SystemAPI.GetSingleton<PlayerRegistry>(),
                NodeLookup = _nodeLookup,
                StockLookup = _stockLookup,
                Reach = _reach,
                FactionLookup = _factionLookup,
                ModifierLookup = _modifierLookup,
                StatsLookup = _statsLookup,
                LocomotionLookup = _locomotionLookup,
                Grid = SystemAPI.GetSingleton<NavGrid>(),
            }.Schedule();
        }

        /// <summary>Single-threaded: harvesters drain shared nodes and stockpiles.</summary>
        [BurstCompile]
        [WithNone(typeof(Dead))]
        [WithPresent(typeof(MoveDestination), typeof(Arrival))]
        private partial struct GatherJob : IJobEntity
        {
            public float DeltaTime;
            [ReadOnly] public NativeArray<Entity> Nodes;
            [ReadOnly] public NativeArray<Entity> DropOffs;
            public PlayerRegistry Players;
            public ComponentLookup<ResourceNode> NodeLookup;
            public BufferLookup<ResourceStock> StockLookup;
            public TargetReach Reach;
            [ReadOnly] public ComponentLookup<Faction> FactionLookup;
            [ReadOnly] public BufferLookup<StatModifier> ModifierLookup;
            public ComponentLookup<PlayerStats> StatsLookup;
            [ReadOnly] public ComponentLookup<Locomotion> LocomotionLookup;
            [ReadOnly] public NavGrid Grid;

            private void Execute(Entity entity, ref Harvester harvester, ref HarvestState harvest,
                ref ActiveOrder order, EnabledRefRW<ActiveOrder> busy, ref MoveDestination destination,
                EnabledRefRW<MoveDestination> moving, EnabledRefRW<Arrival> arrived, in LocalTransform transform,
                in NavAgent agent, in Faction faction)
            {
                if (!busy.ValueRO || order.Value.Type != OrderType.Gather)
                {
                    return;
                }

                if (harvest.Phase == HarvestPhase.Returning)
                {
                    Return(entity, ref harvester, ref harvest, ref destination, busy, moving, arrived, transform,
                        agent, faction.Value);
                    return;
                }

                if (!IsHarvestable(order.Value.Target) && !TryReplaceNode(ref order, harvester, harvest))
                {
                    GiveUp(ref harvest, harvester, busy, moving);
                    return;
                }

                var node = order.Value.Target;
                var nodePosition = Reach.Position(node);
                var toNode = Reach.Approach(node, transform, agent, ref destination, moving, arrived);
                if (!ActiveOrder.StopsShort(toNode, busy, moving))
                {
                    Gather(ref harvester, ref harvest, node, nodePosition);
                }
            }

            // Nothing left nearby: deliver what is carried, then the order is done.
            private static void GiveUp(ref HarvestState harvest, in Harvester harvester, EnabledRefRW<ActiveOrder> busy,
                EnabledRefRW<MoveDestination> moving)
            {
                harvest.Phase = HarvestPhase.Returning;
                harvest.DropOff = Entity.Null;
                if (harvester.CargoAmount <= 0)
                {
                    ActiveOrder.Finish(busy, moving);
                }
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
                    harvest.DropOff = Entity.Null;
                }
            }

            private void Return(Entity entity, ref Harvester harvester, ref HarvestState harvest,
                ref MoveDestination destination, EnabledRefRW<ActiveOrder> busy, EnabledRefRW<MoveDestination> moving,
                EnabledRefRW<Arrival> arrived, in LocalTransform transform, in NavAgent agent, byte faction)
            {
                // Empty-handed goes straight back to work.
                if (harvester.CargoAmount <= 0)
                {
                    harvest.Phase = HarvestPhase.Gathering;
                    moving.ValueRW = false;
                    return;
                }

                if (!IsDropOff(harvest.DropOff, faction))
                {
                    harvest.DropOff = ChooseDropOff(entity, transform, agent, faction);
                }

                // With no drop-off, wait until one is built.
                var dropOff = harvest.DropOff;
                if (dropOff == Entity.Null)
                {
                    moving.ValueRW = false;
                    return;
                }

                var walk = Reach.Approach(dropOff, transform, agent, ref destination, moving, arrived);
                if (ActiveOrder.StopsShort(walk, busy, moving))
                {
                    return;
                }

                var player = Players[faction];
                var delivered = ResourcePayout.Scaled(ModifierLookup, player, harvester.CargoAmount);
                ResourcePayout.Pay(ref StockLookup, ref StatsLookup, player, harvester.CargoType, delivered);
                harvester.CargoAmount = 0;
                harvest.Phase = HarvestPhase.Gathering;
            }

            private bool IsHarvestable(Entity node) =>
                NodeLookup.TryGetComponent(node, out var data) && data.Amount > 0 && Reach.Exists(node);

            /// <summary>Retargets to the nearest node of the carried type near the depleted one.</summary>
            private bool TryReplaceNode(ref ActiveOrder order, in Harvester harvester, in HarvestState harvest)
            {
                var closest = Closest.Within(harvester.ReplacementRange * harvester.ReplacementRange);
                for (var i = 0; i < Nodes.Length; i++)
                {
                    var candidate = Nodes[i];
                    var node = NodeLookup[candidate];
                    if (node.Amount <= 0 || !node.Type.Equals(harvester.CargoType))
                    {
                        continue;
                    }

                    closest.Consider(i, candidate, DistanceSq(candidate, harvest.NodePosition));
                }

                order.Value.Target = closest.Entity;
                return closest.Found;
            }

            /// <summary>Still a finished, living drop-off of this owner.</summary>
            private readonly bool IsDropOff(Entity entity, byte faction) =>
                DropOffs.Contains(entity) && FactionLookup[entity].Value == faction;

            /// <summary>The reachable owned drop-off with the lowest edge gap plus the path its turn takes.</summary>
            private Entity ChooseDropOff(Entity entity, in LocalTransform transform, in NavAgent agent, byte faction)
            {
                var position = transform.Position;
                var facing = math.normalizesafe(transform.Forward().xz);
                var turnLength = TurnLength(entity, agent);
                var closest = Closest.None;
                for (var i = 0; i < DropOffs.Length; i++)
                {
                    var candidate = DropOffs[i];
                    if (FactionLookup[candidate].Value != faction)
                    {
                        continue;
                    }

                    // Scores first so only a drop-off that would win pays for the reachability search.
                    var footprint = Reach.Footprint(candidate);
                    var edge = footprint.EdgePoint(position.xz, agent.Radius);
                    var toEdge = math.normalizesafe(edge - position.xz);
                    var turn = math.acos(math.clamp(math.dot(facing, toEdge), -1f, 1f));
                    var cost = footprint.Gap(position.xz) + turn * turnLength;
                    if (closest.IsCloserThanBest(cost, candidate) &&
                        Grid.IsReachable(agent, position, new float3(edge.x, position.y, edge.y)))
                    {
                        closest.Consider(i, candidate, cost);
                    }
                }

                return closest.Entity;
            }

            // Metres a radian of turn costs: the turn radius, or half the hull for one that pivots on its tracks.
            private readonly float TurnLength(Entity entity, in NavAgent agent)
            {
                var handling = LocomotionLookup.TryGetComponent(entity, out var own) ? own : Locomotion.Instant;
                return handling.Pivots ? agent.HalfLength : handling.TurnRadius;
            }

            private readonly float DistanceSq(Entity entity, float3 position) =>
                math.distancesq(Reach.Position(entity).xz, position.xz);
        }
    }
}
