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
    /// Only <see cref="BuildingRules.IsSellable"/> buildings sell: not a neutral garrison its occupants hold.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(OrderSystemGroup))]
    public partial struct SellSystem : ISystem
    {
        private CommandReader _commands;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            var selected = SystemAPI.QueryBuilder().WithAll<BuildingTag, Selected, Faction>().WithNone<Dead>().Build();
            _commands = new CommandReader(ref state, TypeSet.Single(CommandType.Sell), selected);
            state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();
            state.RequireForUpdate<MatchRules>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var issued = _commands.Read(ref state);
            if (issued.IsEmpty)
            {
                return;
            }

            var refund = SystemAPI.GetSingleton<MatchRules>().SellRefund;
            var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
                .CreateCommandBuffer(state.WorldUnmanaged);
            var sold = new NativeHashSet<Entity>(8, Allocator.Temp);
            foreach (var each in issued)
            {
                var stock = SystemAPI.GetBuffer<ResourceStock>(each.Player);
                foreach (var building in _commands.Subjects(ref state, each))
                {
                    if (IsSellableBuilding(ref state, building) && sold.Add(building))
                    {
                        Sell(ref state, building, stock, refund);
                        ecb.DestroyEntity(building);
                    }
                }
            }
        }

        private static bool IsSellableBuilding(ref SystemState state, Entity entity) =>
            BuildingRules.IsSellable(state.EntityManager, entity) && !state.EntityManager.HasEnabled<Dead>(entity);

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
