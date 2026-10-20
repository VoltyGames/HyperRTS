using HyperRTS.Core;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Navigation;
using HyperRTS.Simulation.Orders;
using HyperRTS.Simulation.Resources;
using HyperRTS.Simulation.Selection;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace HyperRTS.Simulation.Production
{
    /// <summary>
    /// Consumes producer commands: Produce, CancelProduction, and SetRallyPoint/Smart for the player's producers.
    /// Units are commanded elsewhere.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(OrderSystemGroup))]
    public partial struct ProductionCommandSystem : ISystem
    {
        private CommandReader _commands;
        private EntityQuery _completed;
        private EntityQuery _queues;
        private EntityQuery _limited;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            var selected = SystemAPI.QueryBuilder().WithAll<Producer, ProductionOption, Faction, Selected>()
                .WithNone<Dead>().Build();
            _commands = new CommandReader(ref state, ProducerCommands, selected);
            _completed = CompletedBuildings.Query(Allocator.Temp).Build(ref state);
            _queues = ResearchRules.QueueQuery(Allocator.Temp).Build(ref state);
            _limited = BuildCounts.Query(Allocator.Temp).Build(ref state);
            state.RequireForUpdate<NavGrid>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // Most frames carry no producer commands; skip fetching writable stockpiles.
            var issued = _commands.Read(ref state);
            if (issued.IsEmpty)
            {
                return;
            }

            var buildings = new CompletedBuildings(_completed, Allocator.Temp);
            var buyer = default(Buyer);
            var buyerPlayer = Entity.Null;
            foreach (var each in issued)
            {
                // One buyer per player for the frame, so each purchase counts against the next one's limits.
                if (each.Player != buyerPlayer)
                {
                    buyer = Buyer.Snapshot(state.EntityManager, _queues, _limited, each.Faction, buildings,
                        SystemAPI.GetBuffer<CompletedResearch>(each.Player));
                    buyerPlayer = each.Player;
                }

                Execute(ref state, each, buyer);
            }
        }

        private void Execute(ref SystemState state, in IssuedCommand issued, in Buyer buyer)
        {
            switch (issued.Command.Type)
            {
                case CommandType.Produce:
                    Produce(ref state, issued, buyer);
                    break;
                case CommandType.CancelProduction:
                    Cancel(ref state, issued, buyer);
                    break;
                default:
                    SetRallyPoint(ref state, issued);
                    break;
            }
        }

        private static TypeSet ProducerCommands =>
            TypeSet.Range(CommandType.Produce, CommandType.SetRallyPoint).With(CommandType.Smart);

        private void Produce(ref SystemState state, in IssuedCommand issued, in Buyer buyer)
        {
            var producer = FindProducer(ref state, issued);
            if (producer == Entity.Null)
            {
                return;
            }

            var queue = SystemAPI.GetBuffer<ProductionQueueItem>(producer);
            var prefab = issued.Command.Prefab;
            var stock = SystemAPI.GetBuffer<ResourceStock>(issued.Player);
            if (queue.Length >= SystemAPI.GetComponent<Producer>(producer).QueueLimit)
            {
                return;
            }

            if (!PurchaseRules.TryBuy(state.EntityManager, prefab, buyer, stock))
            {
                return;
            }

            var typeId = EntityInfo.GetTypeId(state.EntityManager, prefab);
            queue.Add(new ProductionQueueItem { Prefab = prefab, TypeId = typeId });
            buyer.AddPurchase(state.EntityManager, prefab);
        }

        private void Cancel(ref SystemState state, in IssuedCommand issued, in Buyer buyer)
        {
            var producer = FirstWithQueue(ref state, issued);
            if (producer == Entity.Null)
            {
                return;
            }

            var queue = SystemAPI.GetBuffer<ProductionQueueItem>(producer);
            var argument = issued.Command.Argument;
            var index = argument < 0 ? queue.Length - 1 : argument;
            if (index < 0 || index >= queue.Length)
            {
                return;
            }

            var prefab = queue[index].Prefab;
            queue.RemoveAt(index);
            // Research is queued once per player, so the cancelled one is free to queue again this frame.
            buyer.RemovePurchase(state.EntityManager, prefab);
            if (index == 0)
            {
                SystemAPI.GetComponentRW<Producer>(producer).ValueRW.Elapsed = 0f;
            }

            var stock = SystemAPI.GetBuffer<ResourceStock>(issued.Player);
            ProductionRules.Refund(stock, prefab, SystemAPI.GetBufferLookup<ResourceCost>(true));
        }

        private void SetRallyPoint(ref SystemState state, in IssuedCommand issued)
        {
            var position = issued.Command.Position;
            position.y = SystemAPI.GetSingleton<NavGrid>().SurfaceHeight(position);
            foreach (var producer in _commands.Subjects(ref state, issued))
            {
                if (SystemAPI.HasComponent<Producer>(producer))
                {
                    SystemAPI.SetComponent(producer, new RallyPoint { Position = position });
                    SystemAPI.SetComponentEnabled<RallyPoint>(producer, true);
                }
            }
        }

        /// <summary>The commanded producer, or the selected one with the shortest queue that offers the prefab.</summary>
        private Entity FindProducer(ref SystemState state, in IssuedCommand issued)
        {
            var best = Entity.Null;
            var shortest = int.MaxValue;
            foreach (var producer in _commands.Subjects(ref state, issued))
            {
                if (!CanProduce(ref state, producer, issued.Command.Prefab))
                {
                    continue;
                }

                var length = SystemAPI.GetBuffer<ProductionQueueItem>(producer).Length;
                if (length < shortest)
                {
                    best = producer;
                    shortest = length;
                }
            }

            return best;
        }

        /// <summary>A finished producer offering <paramref name="prefab"/>.</summary>
        private bool CanProduce(ref SystemState state, Entity producer, Entity prefab)
        {
            if (!SystemAPI.HasComponent<Producer>(producer))
            {
                return false;
            }

            if (state.EntityManager.HasEnabled<ConstructionProgress>(producer))
            {
                return false;
            }

            return ProductionRules.HasOption(SystemAPI.GetBuffer<ProductionOption>(producer), prefab);
        }

        /// <summary>The first commanded producer that has something queued.</summary>
        private Entity FirstWithQueue(ref SystemState state, in IssuedCommand issued)
        {
            foreach (var producer in _commands.Subjects(ref state, issued))
            {
                if (!SystemAPI.HasComponent<Producer>(producer))
                {
                    continue;
                }

                if (!SystemAPI.GetBuffer<ProductionQueueItem>(producer).IsEmpty)
                {
                    return producer;
                }
            }

            return Entity.Null;
        }
    }
}
