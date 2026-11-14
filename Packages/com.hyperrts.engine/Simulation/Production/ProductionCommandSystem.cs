using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Common.Geometry;
using HyperRTS.Simulation.Common.Systems;
using HyperRTS.Simulation.Common.Players;
using HyperRTS.Simulation.Navigation.Grid;
using HyperRTS.Simulation.Orders;
using HyperRTS.Simulation.Resources;
using HyperRTS.Simulation.Selection;
using Unity.Burst;
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
        private BuyerQueries _buyers;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            var selected = SystemAPI.QueryBuilder().WithAll<Producer, ProductionOption, Owner, Selected>().Build();
            _commands = new CommandReader(ref state, ProducerCommands, selected);
            _buyers = new BuyerQueries(ref state);
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

            var buyer = default(Buyer);
            var buyerPlayer = Entity.Null;
            foreach (var each in issued)
            {
                // One buyer per player for the frame, so each purchase counts against the next one's limits.
                if (each.Player != buyerPlayer)
                {
                    buyer = _buyers.Snapshot(state.EntityManager, each.Player);
                    buyerPlayer = each.Player;
                }

                Execute(ref state, each, ref buyer);
            }
        }

        private void Execute(ref SystemState state, in IssuedCommand issued, ref Buyer buyer)
        {
            switch (issued.Command.Type)
            {
                case CommandType.Produce:
                    Produce(ref state, issued, ref buyer);
                    break;
                case CommandType.CancelProduction:
                    Cancel(ref state, issued, ref buyer);
                    break;
                default:
                    SetRallyPoint(ref state, issued);
                    break;
            }
        }

        private static TypeSet ProducerCommands =>
            TypeSet.Single(CommandType.Produce).With(CommandType.CancelProduction).With(CommandType.SetRallyPoint)
                .With(CommandType.Smart);

        private void Produce(ref SystemState state, in IssuedCommand issued, ref Buyer buyer)
        {
            var producer = FindProducer(ref state, issued);
            if (producer == Entity.Null)
            {
                return;
            }

            var queue = SystemAPI.GetBuffer<ProductionQueueItem>(producer);
            var prefab = issued.Command.Prefab;
            var stock = new Stockpile(SystemAPI.GetBuffer<ResourceStock>(issued.Player));
            if (!buyer.TryBuy(state.EntityManager, prefab, stock))
            {
                return;
            }

            var typeId = EntityInfo.GetTypeId(state.EntityManager, prefab);
            queue.Add(new ProductionQueueItem { Prefab = prefab, TypeId = typeId });
            buyer.AddPurchase(state.EntityManager, prefab);
        }

        private void Cancel(ref SystemState state, in IssuedCommand issued, ref Buyer buyer)
        {
            var producer = FirstWithQueue(ref state, issued);
            if (producer == Entity.Null)
            {
                return;
            }

            var queue = new ProductionQueue(SystemAPI.GetBuffer<ProductionQueueItem>(producer));
            var trainer = SystemAPI.GetComponentRW<Producer>(producer);
            if (!queue.TryCancel(issued.Command.Argument, ref trainer.ValueRW, out var prefab))
            {
                return;
            }

            // Research is queued once per player, so the cancelled one is free to queue again this frame.
            buyer.RemovePurchase(state.EntityManager, prefab);
            var stock = new Stockpile(SystemAPI.GetBuffer<ResourceStock>(issued.Player));
            stock.Refund(prefab, SystemAPI.GetBufferLookup<ResourceCost>(true));
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
            var shortest = Closest.None;
            var producers = _commands.Subjects(ref state, issued);
            for (var i = 0; i < producers.Length; i++)
            {
                var producer = producers[i];
                if (Producer.CanQueue(state.EntityManager, producer, issued.Owner, issued.Command.Prefab))
                {
                    shortest.Consider(i, producer, SystemAPI.GetBuffer<ProductionQueueItem>(producer).Length);
                }
            }

            return shortest.Entity;
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
