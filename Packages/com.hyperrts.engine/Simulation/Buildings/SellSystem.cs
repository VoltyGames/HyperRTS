using HyperRTS.Core;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Match;
using HyperRTS.Simulation.Orders;
using HyperRTS.Simulation.Production;
using HyperRTS.Simulation.Resources;
using HyperRTS.Simulation.Selection;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace HyperRTS.Simulation.Buildings
{
    /// <summary>
    /// Sells the commanded or selected owned buildings: refunds <see cref="MatchRules.SellRefund"/> of a finished
    /// building's cost (all of an unfinished one's) plus its queued production, then removes it without a wreck.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(OrderSystemGroup))]
    public partial struct SellSystem : ISystem
    {
        private EntityQuery _selected;
        private EntityQuery _commanders;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _commanders = PlayerCommands.Query(ref state);
            _selected = SystemAPI.QueryBuilder().WithAll<BuildingTag, Selected, Faction>().WithNone<Dead>().Build();
            state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (!PlayerCommands.Any(ref state, _commanders, PlayerCommands.Mask(CommandType.Sell)))
            {
                return;
            }

            var rules = SystemAPI.TryGetSingleton<MatchRules>(out var match) ? match : MatchRules.Default;
            var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
                .CreateCommandBuffer(state.WorldUnmanaged);
            var sold = new NativeHashSet<Entity>(8, Allocator.Temp);

            foreach (var (player, commands, listed, stock) in SystemAPI
                         .Query<RefRO<Player>, DynamicBuffer<PlayerCommand>, DynamicBuffer<PlayerCommandSubject>,
                             DynamicBuffer<ResourceStock>>()
                         .WithNone<Defeated>())
            {
                foreach (var command in commands)
                {
                    if (command.Type != CommandType.Sell)
                    {
                        continue;
                    }

                    foreach (var building in PlayerCommands.Collect(command, listed, _selected))
                    {
                        if (IsOwnedBuilding(ref state, building, player.ValueRO.Faction) && sold.Add(building))
                        {
                            Sell(ref state, building, stock, rules.SellRefund);
                            ecb.DestroyEntity(building);
                        }
                    }
                }
            }
        }

        private bool IsOwnedBuilding(ref SystemState state, Entity entity, byte faction)
        {
            if (!SystemAPI.HasComponent<BuildingTag>(entity) || state.EntityManager.HasEnabled<Dead>(entity))
            {
                return false;
            }

            return SystemAPI.GetComponent<Faction>(entity).Value == faction;
        }

        private void Sell(ref SystemState state, Entity building, DynamicBuffer<ResourceStock> stock, float refund)
        {
            var unfinished = state.EntityManager.HasEnabled<ConstructionProgress>(building);
            if (SystemAPI.HasBuffer<ResourceCost>(building))
            {
                ResourceMath.Refund(stock, SystemAPI.GetBuffer<ResourceCost>(building), unfinished ? 1f : refund);
            }

            if (SystemAPI.HasBuffer<ProductionQueueItem>(building))
            {
                ProductionRules.RefundQueue(stock, SystemAPI.GetBuffer<ProductionQueueItem>(building),
                    SystemAPI.GetBufferLookup<ResourceCost>(true));
            }
        }
    }
}
