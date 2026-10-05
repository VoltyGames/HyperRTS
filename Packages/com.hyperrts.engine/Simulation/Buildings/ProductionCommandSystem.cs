using HyperRTS.Core;
using HyperRTS.Simulation.Combat;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Match;
using HyperRTS.Simulation.Orders;
using HyperRTS.Simulation.Resources;
using HyperRTS.Simulation.Selection;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace HyperRTS.Simulation.Buildings
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

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _selectedProducers = SystemAPI.QueryBuilder().WithAll<Producer, ProductionOption, Faction, Selected>()
                .WithNone<Dead>().Build();
            _completed = CompletedBuildings.Query(Allocator.Temp).Build(ref state);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // Most frames carry no producer commands; skip fetching writable stockpiles.
            if (!HasProducerCommand(ref state))
            {
                return;
            }

            foreach (var (player, commands, stock) in SystemAPI
                         .Query<RefRO<Player>, DynamicBuffer<PlayerCommand>, DynamicBuffer<ResourceStock>>()
                         .WithNone<Defeated>())
            {
                var faction = player.ValueRO.Faction;
                foreach (var command in commands)
                {
                    switch (command.Type)
                    {
                        case CommandType.Produce:
                            Produce(ref state, faction, command, stock);
                            break;
                        case CommandType.CancelProduction:
                            Cancel(ref state, faction, command, stock);
                            break;
                        case CommandType.SetRallyPoint:
                        case CommandType.Smart:
                            SetRallyPoint(ref state, faction, command);
                            break;
                    }
                }
            }
        }

        private static bool IsProducerCommand(CommandType type) =>
            type is CommandType.Produce or CommandType.CancelProduction or CommandType.SetRallyPoint
                or CommandType.Smart;

        private bool HasProducerCommand(ref SystemState state)
        {
            foreach (var commands in SystemAPI.Query<DynamicBuffer<PlayerCommand>>())
            {
                foreach (var command in commands)
                {
                    if (IsProducerCommand(command.Type))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private void Produce(ref SystemState state, byte faction, in PlayerCommand command,
            DynamicBuffer<ResourceStock> stock)
        {
            var producer = FindProducer(ref state, faction, command);
            if (producer == Entity.Null)
            {
                return;
            }

            var queue = SystemAPI.GetBuffer<ProductionQueueItem>(producer);
            var prefab = command.Prefab;
            if (queue.Length >= SystemAPI.GetComponent<Producer>(producer).QueueLimit ||
                !CompletedBuildings.MeetsPrerequisites(SystemAPI.GetBuffer<Prerequisite>(prefab), faction, _completed) ||
                !ResourceMath.TrySpend(stock, SystemAPI.GetBuffer<ResourceCost>(prefab)))
            {
                return;
            }

            queue.Add(new ProductionQueueItem { Prefab = prefab });
        }

        private void Cancel(ref SystemState state, byte faction, in PlayerCommand command,
            DynamicBuffer<ResourceStock> stock)
        {
            var producer = command.Unit != Entity.Null ? command.Unit : FirstSelectedWithQueue(ref state, faction);
            if (!IsOwnedProducer(ref state, producer, faction))
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

            if (SystemAPI.HasBuffer<ResourceCost>(prefab))
            {
                ResourceMath.Refund(stock, SystemAPI.GetBuffer<ResourceCost>(prefab));
            }
        }

        private void SetRallyPoint(ref SystemState state, byte faction, in PlayerCommand command)
        {
            foreach (var producer in CommandSubjects.Collect(command.Unit, _selectedProducers))
            {
                if (IsOwnedProducer(ref state, producer, faction))
                {
                    SystemAPI.SetComponent(producer, new RallyPoint { Position = command.Position });
                    SystemAPI.SetComponentEnabled<RallyPoint>(producer, true);
                }
            }
        }

        /// <summary>The commanded producer, or the selected one with the shortest queue that offers the prefab.</summary>
        private Entity FindProducer(ref SystemState state, byte faction, in PlayerCommand command)
        {
            var best = Entity.Null;
            var shortest = int.MaxValue;
            foreach (var producer in CommandSubjects.Collect(command.Unit, _selectedProducers))
            {
                if (!IsOwnedProducer(ref state, producer, faction) ||
                    ConstructionRules.IsUnderConstruction(state.EntityManager, producer) ||
                    !ProductionRules.Offers(SystemAPI.GetBuffer<ProductionOption>(producer), command.Prefab))
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

        private Entity FirstSelectedWithQueue(ref SystemState state, byte faction)
        {
            foreach (var producer in _selectedProducers.ToEntityArray(Allocator.Temp))
            {
                if (IsOwnedProducer(ref state, producer, faction) &&
                    !SystemAPI.GetBuffer<ProductionQueueItem>(producer).IsEmpty)
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
