using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Common.Geometry;
using HyperRTS.Simulation.Common.Players;
using HyperRTS.Simulation.Orders;
using HyperRTS.Simulation.Production;
using HyperRTS.Simulation.Resources;
using HyperRTS.Simulation.Resources.Gathering;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.AI
{
    /// <summary>Sends idle harvesters to resource fields and keeps idle producers training.</summary>
    internal struct EconomyPlanner
    {
        /// <summary>A hinted node counts as the field's own when it stands this close to the hint (squared metres).</summary>
        private const float NodeMatchRadiusSq = 4f * 4f;

        private EntityQuery _idleHarvesters;
        private EntityQuery _nodeQuery;
        private AIGroup _harvesters;
        private AIGroup _nodes;
        private NativeArray<ResourceNode> _nodeData;

        public static EconomyPlanner Create(ref SystemState state) => new()
        {
            _idleHarvesters = new EntityQueryBuilder(Allocator.Temp).WithAll<Harvester, Owner, LocalTransform>()
                .WithDisabled<ActiveOrder>().WithNone<Dead>().Build(ref state),
            _nodeQuery = new EntityQueryBuilder(Allocator.Temp).WithAll<ResourceNode, Owner, LocalTransform>()
                .Build(ref state),
        };

        public void Update()
        {
            _harvesters = AIGroup.From(_idleHarvesters);
            _nodes = AIGroup.From(_nodeQuery);
            _nodeData = _nodeQuery.ToComponentDataArray<ResourceNode>(Allocator.Temp);
        }

        /// <summary>Sends idle harvesters to the first field in the slot's expansion order that still has resources.</summary>
        public readonly void SendHarvesters(in AITurn turn, in AISnapshot snapshot)
        {
            var node = -1;
            for (var i = 0; i < _harvesters.Length; i++)
            {
                if (!_harvesters.IsOwnedBy(i, turn.Owner) || turn.Busy.Contains(_harvesters.Entities[i]))
                {
                    continue;
                }

                node = node >= 0 ? node : FirstExpansionNode(turn.Owner, snapshot);
                if (node < 0)
                {
                    return;
                }

                turn.Commands.Add(new PlayerCommand
                {
                    Type = CommandType.Gather,
                    Unit = _harvesters.Entities[i],
                    Target = _nodes.Entities[node],
                    Position = _nodes.Position(node),
                });
            }
        }

        /// <summary>
        /// The live node (index in the nodes) of the first expansion hint that has one: the slot's main field while it
        /// lasts, then each next-nearest by walking distance; -1 when every field is spent.
        /// </summary>
        private readonly int FirstExpansionNode(byte owner, in AISnapshot snapshot)
        {
            var hints = snapshot.Hints;
            if (!hints.TryGetSlot(owner, out var slot))
            {
                return -1;
            }

            for (var k = 0; k < slot.ExpansionCount; k++)
            {
                var node = LiveNodeAt(hints.Point(slot.ExpansionStart + k));
                if (node >= 0)
                {
                    return node;
                }
            }

            return -1;
        }

        private readonly int LiveNodeAt(float2 point)
        {
            var closest = Closest.Within(NodeMatchRadiusSq);
            for (var i = 0; i < _nodes.Length; i++)
            {
                if (_nodeData[i].Amount > 0)
                {
                    closest.Consider(i, _nodes.Entities[i], math.distancesq(_nodes.Position(i).xz, point));
                }
            }

            return closest.Index;
        }

        /// <summary>After the build order: every idle producer trains or researches round-robin.</summary>
        public readonly void Train(ref SystemState state, in AITurn turn, in AISnapshot snapshot)
        {
            var entities = state.EntityManager;
            var ai = entities.GetComponentData<AIPlayer>(turn.Player);
            var budget = new Budget
            {
                Room = entities.GetComponentData<Population>(turn.Player),
                Copy = entities.GetBuffer<ResourceStock>(turn.Player).ToNativeArray(Allocator.Temp),
                Buyer = turn.Buyer,
            };

            foreach (var producer in snapshot.Producers)
            {
                if (!IsIdleProducer(entities, producer, turn.Owner))
                {
                    continue;
                }

                var prefab = PickOption(entities, ref ai, entities.GetBuffer<ProductionOption>(producer), budget);
                if (prefab != Entity.Null)
                {
                    budget.Room.Used += entities.GetComponentData<Producible>(prefab).Population;
                    budget.Stock.Deduct(entities.GetBuffer<ResourceCost>(prefab));
                    turn.Commands.Add(new PlayerCommand { Type = CommandType.Produce, Unit = producer, Prefab = prefab });
                    // As the server does, so a second idle producer doesn't pick the same research this think.
                    budget.Buyer.AddPurchase(entities, prefab);
                }
            }

            entities.SetComponentData(turn.Player, ai);
            budget.Copy.Dispose();
        }

        private static bool IsIdleProducer(EntityManager entities, Entity producer, byte owner)
        {
            if (entities.GetComponentData<Owner>(producer).Value != owner)
            {
                return false;
            }

            return entities.GetBuffer<ProductionQueueItem>(producer).IsEmpty;
        }

        /// <summary>Next option after the last one picked that fits population, stockpile and tech, skipping done or queued research.</summary>
        private static Entity PickOption(EntityManager entities, ref AIPlayer ai, DynamicBuffer<ProductionOption> options,
            in Budget budget)
        {
            for (var k = 0; k < options.Length; k++)
            {
                var index = (math.max(ai.NextOption, 0) + k) % options.Length;
                var prefab = options[index].Prefab;
                if (budget.CanTrain(entities, prefab))
                {
                    ai.NextOption = index + 1;
                    return prefab;
                }
            }

            return Entity.Null;
        }

        private struct Budget
        {
            public Population Room;

            /// <summary>The stockpile left after what this think already queued.</summary>
            public NativeArray<ResourceStock> Copy;
            public readonly Stockpile Stock => new Stockpile(Copy);
            public Buyer Buyer;

            public readonly bool CanTrain(EntityManager entities, Entity prefab)
            {
                if (!Room.HasRoomFor(entities.GetComponentData<Producible>(prefab).Population))
                {
                    return false;
                }

                return Buyer.CanBuy(entities, prefab, Stock);
            }
        }
    }
}
