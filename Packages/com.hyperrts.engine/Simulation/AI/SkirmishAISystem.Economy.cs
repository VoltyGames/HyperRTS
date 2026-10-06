using HyperRTS.Simulation.Buildings;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Match;
using HyperRTS.Simulation.Orders;
using HyperRTS.Simulation.Resources;
using HyperRTS.Simulation.Upgrades;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.AI
{
    public partial struct SkirmishAISystem
    {
        private struct Budget
        {
            public byte Faction;
            public Population Room;
            public DynamicBuffer<ResourceStock> Stock;
            public DynamicBuffer<ResearchedUpgrade> Researched;
            public NativeHashSet<Entity> QueuedUpgrades;
            public CompletedBuildings Completed;
        }

        private static void SendHarvesters(in Turn turn, in Snapshot snapshot)
        {
            var harvesters = snapshot.Harvesters;
            var nodes = snapshot.Nodes;
            for (var i = 0; i < harvesters.Length; i++)
            {
                if (!harvesters.IsOwnedBy(i, turn.Faction) || turn.Busy.Contains(harvesters.Entities[i]))
                {
                    continue;
                }

                var node = NearestNode(nodes, snapshot.NodeData, harvesters.Position(i));
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

        private static int NearestNode(in AIGroup nodes, NativeArray<ResourceNode> data, float3 from)
        {
            var best = -1;
            var bestDistance = float.MaxValue;
            for (var i = 0; i < nodes.Length; i++)
            {
                var distance = math.distancesq(nodes.Position(i).xz, from.xz);
                if (distance < bestDistance && data[i].Amount > 0)
                {
                    best = i;
                    bestDistance = distance;
                }
            }

            return best;
        }

        /// <summary>After the build order: every idle producer trains or researches round-robin.</summary>
        private void Train(ref SystemState state, in Turn turn, in Snapshot snapshot)
        {
            ref var ai = ref SystemAPI.GetComponentRW<AIPlayer>(turn.Player).ValueRW;
            var queued = new NativeHashSet<Entity>(8, Allocator.Temp);
            UpgradeRules.CollectQueued(state.EntityManager, _queues, turn.Faction, queued);
            var budget = new Budget
            {
                Faction = turn.Faction,
                Room = SystemAPI.GetComponent<Population>(turn.Player),
                Stock = SystemAPI.GetBuffer<ResourceStock>(turn.Player),
                Researched = SystemAPI.GetBuffer<ResearchedUpgrade>(turn.Player),
                QueuedUpgrades = queued,
                Completed = snapshot.Completed,
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
                    turn.Commands.Add(new PlayerCommand { Type = CommandType.Produce, Unit = producer, Prefab = prefab });
                }
            }

            queued.Dispose();
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
            if (!UpgradeRules.CanQueue(state.EntityManager, budget.Researched, budget.QueuedUpgrades, prefab))
            {
                return false;
            }

            if (!budget.Room.HasRoomFor(SystemAPI.GetComponent<Producible>(prefab).Population))
            {
                return false;
            }

            if (!ResourceMath.CanAfford(budget.Stock, SystemAPI.GetBuffer<ResourceCost>(prefab)))
            {
                return false;
            }

            return budget.Completed.MeetsPrerequisites(SystemAPI.GetBuffer<Prerequisite>(prefab), budget.Faction);
        }
    }
}
