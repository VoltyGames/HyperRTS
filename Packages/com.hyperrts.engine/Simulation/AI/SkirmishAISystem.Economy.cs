using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Match;
using HyperRTS.Simulation.Orders;
using HyperRTS.Simulation.Production;
using HyperRTS.Simulation.Resources;
using HyperRTS.Simulation.Spatial;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.AI
{
    public partial struct SkirmishAISystem
    {
        private struct Budget
        {
            public Population Room;

            /// <summary>The stockpile left after what this think already queued.</summary>
            public NativeArray<ResourceStock> Stock;
            public Buyer Buyer;
        }

        /// <summary>A hinted node counts as the field's own when it stands this close to the hint (squared metres).</summary>
        private const float NodeMatchRadiusSq = 4f * 4f;

        /// <summary>Sends idle harvesters to the first field in the slot's expansion order that still has resources.</summary>
        private static void SendHarvesters(in Turn turn, in Snapshot snapshot)
        {
            var harvesters = snapshot.Harvesters;
            var nodes = snapshot.Nodes;
            var node = -1;
            for (var i = 0; i < harvesters.Length; i++)
            {
                if (!harvesters.IsOwnedBy(i, turn.Faction) || turn.Busy.Contains(harvesters.Entities[i]))
                {
                    continue;
                }

                node = node >= 0 ? node : FirstExpansionNode(turn.Faction, snapshot);
                if (node < 0)
                {
                    return;
                }

                turn.Commands.Add(new PlayerCommand
                {
                    Type = CommandType.Gather,
                    Unit = harvesters.Entities[i],
                    Target = nodes.Entities[node],
                    Position = nodes.Position(node),
                });
            }
        }

        /// <summary>
        /// The live node (index in the snapshot's nodes) of the first expansion hint that has one: the slot's main
        /// field while it lasts, then each next-nearest by walking distance; -1 when every field is spent.
        /// </summary>
        private static int FirstExpansionNode(byte faction, in Snapshot snapshot)
        {
            var hints = snapshot.Hints;
            if (!hints.TryGetSlot(faction, out var slot))
            {
                return -1;
            }

            for (var k = 0; k < slot.ExpansionCount; k++)
            {
                var node = LiveNodeAt(snapshot, hints.Point(slot.ExpansionStart + k));
                if (node >= 0)
                {
                    return node;
                }
            }

            return -1;
        }

        private static int LiveNodeAt(in Snapshot snapshot, float2 point)
        {
            var nodes = snapshot.Nodes;
            var closest = Closest.Within(NodeMatchRadiusSq);
            for (var i = 0; i < nodes.Length; i++)
            {
                if (snapshot.NodeData[i].Amount > 0)
                {
                    closest.Consider(i, nodes.Entities[i], math.distancesq(nodes.Position(i).xz, point));
                }
            }

            return closest.Index;
        }

        /// <summary>After the build order: every idle producer trains or researches round-robin.</summary>
        private void Train(ref SystemState state, in Turn turn, in Snapshot snapshot)
        {
            ref var ai = ref SystemAPI.GetComponentRW<AIPlayer>(turn.Player).ValueRW;
            var budget = new Budget
            {
                Room = SystemAPI.GetComponent<Population>(turn.Player),
                Stock = SystemAPI.GetBuffer<ResourceStock>(turn.Player).ToNativeArray(Allocator.Temp),
                Buyer = turn.Buyer,
            };

            foreach (var producer in snapshot.Producers)
            {
                if (!IsIdleProducer(ref state, producer, turn.Faction))
                {
                    continue;
                }

                var prefab = PickOption(ref state, ref ai, SystemAPI.GetBuffer<ProductionOption>(producer), budget);
                if (prefab != Entity.Null)
                {
                    budget.Room.Used += SystemAPI.GetComponent<Producible>(prefab).Population;
                    ResourceMath.Deduct(budget.Stock, SystemAPI.GetBuffer<ResourceCost>(prefab));
                    turn.Commands.Add(new PlayerCommand { Type = CommandType.Produce, Unit = producer, Prefab = prefab });
                    // As the server does, so a second idle producer doesn't pick the same research this think.
                    budget.Buyer.QueuedResearch.Add(prefab);
                    budget.Buyer.Counts.Add(state.EntityManager, prefab);
                }
            }

            budget.Stock.Dispose();
        }

        private bool IsIdleProducer(ref SystemState state, Entity producer, byte faction)
        {
            if (SystemAPI.GetComponent<Faction>(producer).Value != faction)
            {
                return false;
            }

            return SystemAPI.GetBuffer<ProductionQueueItem>(producer).IsEmpty;
        }

        /// <summary>Next option after the last one picked that fits population, stockpile and tech, skipping done or queued research.</summary>
        private Entity PickOption(ref SystemState state, ref AIPlayer ai, DynamicBuffer<ProductionOption> options,
            in Budget budget)
        {
            for (var k = 0; k < options.Length; k++)
            {
                var index = (math.max(ai.NextOption, 0) + k) % options.Length;
                var prefab = options[index].Prefab;
                if (CanTrain(ref state, prefab, budget))
                {
                    ai.NextOption = index + 1;
                    return prefab;
                }
            }

            return Entity.Null;
        }

        private bool CanTrain(ref SystemState state, Entity prefab, in Budget budget)
        {
            if (!budget.Room.HasRoomFor(SystemAPI.GetComponent<Producible>(prefab).Population))
            {
                return false;
            }

            return PurchaseRules.CanBuy(state.EntityManager, prefab, budget.Buyer, budget.Stock);
        }
    }
}
