using HyperRTS.Core;
using HyperRTS.Simulation.Buildings;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Navigation;
using HyperRTS.Simulation.Orders;
using HyperRTS.Simulation.Resources;
using HyperRTS.Simulation.Selection;
using HyperRTS.Simulation.Upgrades;
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
        private EntityQuery _selectedProducers;
        private EntityQuery _completed;
        private EntityQuery _queues;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _selectedProducers = SystemAPI.QueryBuilder().WithAll<Producer, ProductionOption, Faction, Selected>()
                .WithNone<Dead>().Build();
            _completed = CompletedBuildings.Query(Allocator.Temp).Build(ref state);
            _queues = UpgradeRules.QueueQuery(Allocator.Temp).Build(ref state);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // Most frames carry no producer commands; skip fetching writable stockpiles.
            if (!PlayerCommands.Any(ref state, ProducerCommands))
            {
                return;
            }

            foreach (var (player, commands, listed, stock, researched) in SystemAPI
                         .Query<RefRO<Player>, DynamicBuffer<PlayerCommand>, DynamicBuffer<PlayerCommandSubject>,
                             DynamicBuffer<ResourceStock>, DynamicBuffer<ResearchedUpgrade>>()
                         .WithNone<Defeated>())
            {
                var faction = player.ValueRO.Faction;
                foreach (var command in commands)
                {
                    switch (command.Type)
                    {
                        case CommandType.Produce:
                            Produce(ref state, faction, command, listed, stock, researched);
                            break;
                        case CommandType.CancelProduction:
                            Cancel(ref state, faction, command, listed, stock);
                            break;
                        case CommandType.SetRallyPoint:
                        case CommandType.Smart:
                            SetRallyPoint(ref state, faction, command, listed);
                            break;
                    }
                }
            }
        }

        private static ulong ProducerCommands =>
            PlayerCommands.Mask(CommandType.Produce, CommandType.SetRallyPoint) | PlayerCommands.Mask(CommandType.Smart);

        private void Produce(ref SystemState state, byte faction, in PlayerCommand command,
            DynamicBuffer<PlayerCommandSubject> listed, DynamicBuffer<ResourceStock> stock,
            DynamicBuffer<ResearchedUpgrade> researched)
        {
            if (!UpgradeRules.CanQueue(state.EntityManager, _queues, researched, faction, command.Prefab))
            {
                return;
            }

            var producer = FindProducer(ref state, faction, command, listed);
            if (producer == Entity.Null)
            {
                return;
            }

            var queue = SystemAPI.GetBuffer<ProductionQueueItem>(producer);
            var prefab = command.Prefab;
            if (queue.Length >= SystemAPI.GetComponent<Producer>(producer).QueueLimit)
            {
                return;
            }

            var required = SystemAPI.GetBuffer<Prerequisite>(prefab);
            if (!CompletedBuildings.MeetsPrerequisites(required, faction, _completed))
            {
                return;
            }

            // Spent last, so a refused order costs nothing.
            if (!ResourceMath.TrySpend(stock, SystemAPI.GetBuffer<ResourceCost>(prefab)))
            {
                return;
            }

            var typeId = EntityInfo.TypeIdOf(state.EntityManager, prefab);
            queue.Add(new ProductionQueueItem { Prefab = prefab, TypeId = typeId });
        }

        private void Cancel(ref SystemState state, byte faction, in PlayerCommand command,
            DynamicBuffer<PlayerCommandSubject> listed, DynamicBuffer<ResourceStock> stock)
        {
            var producer = FirstWithQueue(ref state, faction, command, listed);
            if (producer == Entity.Null)
            {
                return;
            }

            var queue = SystemAPI.GetBuffer<ProductionQueueItem>(producer);
            var index = command.Argument < 0 ? queue.Length - 1 : command.Argument;
            if (index < 0 || index >= queue.Length)
            {
                return;
            }

            var prefab = queue[index].Prefab;
            queue.RemoveAt(index);
            if (index == 0)
            {
                SystemAPI.GetComponentRW<Producer>(producer).ValueRW.Elapsed = 0f;
            }

            ProductionRules.Refund(stock, prefab, SystemAPI.GetBufferLookup<ResourceCost>(true));
        }

        private void SetRallyPoint(ref SystemState state, byte faction, in PlayerCommand command,
            DynamicBuffer<PlayerCommandSubject> listed)
        {
            SystemAPI.TryGetSingleton<NavGrid>(out var grid);
            var position = command.Position;
            position.y = PlacementMath.Height(grid, position);
            foreach (var producer in PlayerCommands.Collect(command, listed, _selectedProducers))
            {
                if (IsOwnedProducer(ref state, producer, faction))
                {
                    SystemAPI.SetComponent(producer, new RallyPoint { Position = position });
                    SystemAPI.SetComponentEnabled<RallyPoint>(producer, true);
                }
            }
        }

        /// <summary>The commanded producer, or the selected one with the shortest queue that offers the prefab.</summary>
        private Entity FindProducer(ref SystemState state, byte faction, in PlayerCommand command,
            DynamicBuffer<PlayerCommandSubject> listed)
        {
            var best = Entity.Null;
            var shortest = int.MaxValue;
            foreach (var producer in PlayerCommands.Collect(command, listed, _selectedProducers))
            {
                if (!CanProduce(ref state, producer, faction, command.Prefab))
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

        /// <summary>An owned, finished producer offering <paramref name="prefab"/>.</summary>
        private bool CanProduce(ref SystemState state, Entity producer, byte faction, Entity prefab)
        {
            if (!IsOwnedProducer(ref state, producer, faction))
            {
                return false;
            }

            if (state.EntityManager.HasEnabled<ConstructionProgress>(producer))
            {
                return false;
            }

            return ProductionRules.Offers(SystemAPI.GetBuffer<ProductionOption>(producer), prefab);
        }

        /// <summary>The first commanded producer the faction owns that has something queued.</summary>
        private Entity FirstWithQueue(ref SystemState state, byte faction, in PlayerCommand command,
            DynamicBuffer<PlayerCommandSubject> listed)
        {
            foreach (var producer in PlayerCommands.Collect(command, listed, _selectedProducers))
            {
                if (!IsOwnedProducer(ref state, producer, faction))
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

        private bool IsOwnedProducer(ref SystemState state, Entity entity, byte faction) =>
            SystemAPI.HasComponent<Producer>(entity) && SystemAPI.GetComponent<Faction>(entity).Value == faction;
    }
}
