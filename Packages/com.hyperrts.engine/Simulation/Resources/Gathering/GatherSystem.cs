using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Common.Buildings;
using HyperRTS.Simulation.Common.Geometry;
using HyperRTS.Simulation.Common.Systems;
using HyperRTS.Simulation.Common.Players;
using HyperRTS.Simulation.Navigation.Agents;
using HyperRTS.Simulation.Navigation.Grid;
using HyperRTS.Simulation.Navigation.Reach;
using HyperRTS.Simulation.Orders;
using HyperRTS.Simulation.Stats;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.Resources.Gathering
{
    /// <summary>
    /// Runs Gather orders: walk to the node, fill up, deposit at an owned drop-off chosen once per trip by its edge
    /// distance and the turn needed to face it, repeat. Every harvester works from any side of the node and drop-off.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(ProductionSystemGroup))]
    public partial struct GatherSystem : ISystem
    {
        private EntityQuery _nodes;
        private EntityQuery _dropOffs;
        private EntityQuery _harvesters;
        private TargetReach _reach;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _nodes = SystemAPI.QueryBuilder().WithAll<ResourceNode, LocalTransform>().Build();
            _dropOffs = SystemAPI.QueryBuilder().WithAll<ResourceDropOff, LocalTransform, Owner>()
                .WithNone<ConstructionProgress, Dead>().Build();
            state.RequireForUpdate<PlayerRegistry>();
            _reach = new TargetReach(ref state);
            state.RequireForUpdate<MapSettings>();
            state.RequireForUpdate<NavGrid>();
            _harvesters = SystemAPI.QueryBuilder().WithAll<Harvester, ActiveOrder>().Build();
            state.RequireForUpdate(_harvesters);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var allocator = state.WorldUpdateAllocator;
            var nodes = _nodes.ToEntityListAsync(allocator, state.Dependency, out var nodesReady);
            var dropOffs = _dropOffs.ToEntityListAsync(allocator, state.Dependency, out var dropOffsReady);
            state.Dependency = JobHandle.CombineDependencies(nodesReady, dropOffsReady);

            _reach.Update(ref state, SystemAPI.GetSingleton<MapSettings>());
            var spots = new NativeParallelMultiHashMap<Entity, ReachClaims.Claim>(
                _harvesters.CalculateEntityCountWithoutFiltering() * 2 + 1, allocator);
            state.Dependency = new GatherClaimJob { Spots = spots.AsParallelWriter(), Reach = _reach }
                .ScheduleParallel(state.Dependency);

            new GatherJob
            {
                DeltaTime = SystemAPI.Time.DeltaTime,
                Nodes = nodes.AsDeferredJobArray(),
                DropOffs = dropOffs.AsDeferredJobArray(),
                Players = SystemAPI.GetSingleton<PlayerRegistry>(),
                NodeLookup = SystemAPI.GetComponentLookup<ResourceNode>(false),
                StockLookup = SystemAPI.GetBufferLookup<ResourceStock>(false),
                Reach = _reach,
                Claims = new ReachClaims(spots),
                OwnerLookup = SystemAPI.GetComponentLookup<Owner>(true),
                ModifierLookup = SystemAPI.GetBufferLookup<StatModifier>(true),
                StatsLookup = SystemAPI.GetComponentLookup<PlayerStats>(false),
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
            public ReachClaims Claims;
            [ReadOnly] public ComponentLookup<Owner> OwnerLookup;
            [ReadOnly] public BufferLookup<StatModifier> ModifierLookup;
            public ComponentLookup<PlayerStats> StatsLookup;
            [ReadOnly] public NavGrid Grid;

            private void Execute(Entity entity, ref Harvester harvester, ref HarvestState harvest,
                ref ActiveOrder order, EnabledRefRW<ActiveOrder> busy, RefRW<MoveDestination> destination,
                EnabledRefRW<MoveDestination> moving, ref Arrival arrival, EnabledRefRW<Arrival> arrived,
                in LocalTransform transform, in NavAgent agent, in Owner owner)
            {
                harvest.Filling = false;
                if (!busy.ValueRO || order.Value.Type != OrderType.Gather)
                {
                    harvest.CancelUnload();
                    return;
                }

                var move = new MoveControl(destination, moving, arrived) { Reason = arrival.Reason };
                if (harvest.Phase != HarvestPhase.Gathering)
                {
                    var atDropOff = harvest.Phase == HarvestPhase.Returning && harvester.CargoAmount > 0
                        && ReachedDropOff(entity, ref harvest, move, transform, agent);
                    Deliver(ref harvester, ref harvest, move, owner.Value, atDropOff);
                    return;
                }

                if (!IsHarvestable(order.Value.Target) && !TryReplaceNode(ref order, harvester, harvest))
                {
                    GiveUp(ref harvest, harvester, busy, moving);
                    return;
                }

                var node = order.Value.Target;
                var nodePosition = Reach.Position(node);
                var toNode = Reach.Approach(node, transform, agent, move, Grid, Claims.For(entity));
                // A stalled walk-up is walked again, as an idle harvester earns nothing.
                if (move.HasReached(toNode))
                {
                    Gather(ref harvester, ref harvest, node, nodePosition);
                }
            }

            // Walks back, stands unloading, and is paid when the unload time ends.
            private void Deliver(ref Harvester harvester, ref HarvestState harvest, MoveControl move, byte owner,
                bool atDropOff)
            {
                if (harvest.Phase == HarvestPhase.Unloading)
                {
                    Unload(ref harvester, ref harvest, owner);
                }
                else if (harvester.CargoAmount <= 0)
                {
                    Resume(ref harvest, move);
                }
                else if (atDropOff)
                {
                    Arrive(ref harvester, ref harvest, owner);
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
                harvest.NodePosition = position;
                harvester.Fill(ref harvest, ref NodeLookup.GetRefRW(entity).ValueRW, DeltaTime);
            }

            // Empty-handed goes straight back to work.
            private static void Resume(ref HarvestState harvest, MoveControl move)
            {
                harvest.Phase = HarvestPhase.Gathering;
                move.Moving.ValueRW = false;
            }

            // True once the harvester stands at its drop-off; with none, it waits until one is built.
            private bool ReachedDropOff(Entity entity, ref HarvestState harvest, MoveControl move,
                in LocalTransform transform, in NavAgent agent)
            {
                var owner = OwnerLookup[entity].Value;
                if (!IsDropOff(harvest.DropOff, owner))
                {
                    harvest.DropOff = ChooseDropOff(entity, transform, agent, owner);
                }

                var dropOff = harvest.DropOff;
                if (dropOff == Entity.Null)
                {
                    move.Moving.ValueRW = false;
                    return false;
                }

                var walk = Reach.Approach(dropOff, transform, agent, move, Grid, Claims.For(entity));
                return move.HasReached(walk);
            }

            private void Arrive(ref Harvester harvester, ref HarvestState harvest, byte owner)
            {
                if (harvester.UnloadSeconds > 0f)
                {
                    harvest.BeginUnload();
                    return;
                }

                Pay(ref harvester, ref harvest, owner);
            }

            // A drop-off lost mid-unload sends the truck to find another with its cargo untouched.
            private void Unload(ref Harvester harvester, ref HarvestState harvest, byte owner)
            {
                if (!IsDropOff(harvest.DropOff, owner))
                {
                    harvest.CancelUnload();
                    return;
                }

                harvest.UnloadTime += DeltaTime;
                if (harvest.UnloadTime >= harvester.UnloadSeconds)
                {
                    Pay(ref harvester, ref harvest, owner);
                }
            }

            private void Pay(ref Harvester harvester, ref HarvestState harvest, byte owner)
            {
                var player = Players[owner];
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
            private readonly bool IsDropOff(Entity entity, byte owner) =>
                DropOffs.Contains(entity) && OwnerLookup[entity].Value == owner;

            /// <summary>The reachable owned drop-off with the lowest edge gap plus the path its turn takes.</summary>
            private Entity ChooseDropOff(Entity entity, in LocalTransform transform, in NavAgent agent, byte owner)
            {
                var position = transform.Position;
                var facing = math.normalizesafe(transform.Forward().xz);
                var turnLength = agent.Chassis.TurnLength(agent.HalfLength);
                var closest = Closest.None;
                for (var i = 0; i < DropOffs.Length; i++)
                {
                    var candidate = DropOffs[i];
                    if (OwnerLookup[candidate].Value != owner)
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

            private readonly float DistanceSq(Entity entity, float3 position) =>
                math.distancesq(Reach.Position(entity).xz, position.xz);
        }
    }
}
