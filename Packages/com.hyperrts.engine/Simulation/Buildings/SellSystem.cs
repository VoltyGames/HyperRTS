using HyperRTS.Core;
using HyperRTS.Simulation.Combat;
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
    /// Sells the commanded or selected owned buildings: refunds <see cref="MatchRules.SellRefund"/> of a finished
    /// building's cost (all of an unfinished one's) plus its queued production, then removes it without a wreck.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(OrderSystemGroup))]
    public partial struct SellSystem : ISystem
    {
        private EntityQuery _selected;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _selected = SystemAPI.QueryBuilder().WithAll<BuildingTag, Selected, Faction>().WithNone<Dead>().Build();
            state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (!HasSellCommand(ref state))
            {
                return;
            }

            var refund = SystemAPI.TryGetSingleton<MatchRules>(out var rules) ? rules.SellRefund : MatchRules.Default.SellRefund;
            var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
                .CreateCommandBuffer(state.WorldUnmanaged);
            var sold = new NativeHashSet<Entity>(8, Allocator.Temp);

            foreach (var (player, commands, stock) in SystemAPI
                         .Query<RefRO<Player>, DynamicBuffer<PlayerCommand>, DynamicBuffer<ResourceStock>>()
                         .WithNone<Defeated>())
            {
                foreach (var command in commands)
                {
                    if (command.Type != CommandType.Sell)
                    {
                        continue;
                    }

                    foreach (var building in CommandSubjects.Collect(command.Unit, _selected))
                    {
                        if (IsOwnedBuilding(ref state, building, player.ValueRO.Faction) && sold.Add(building))
                        {
                            Sell(ref state, building, stock, refund);
                            ecb.DestroyEntity(building);
                        }
                    }
                }
            }
        }

        private bool HasSellCommand(ref SystemState state)
        {
            foreach (var commands in SystemAPI.Query<DynamicBuffer<PlayerCommand>>())
            {
                foreach (var command in commands)
                {
                    if (command.Type == CommandType.Sell)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private bool IsOwnedBuilding(ref SystemState state, Entity entity, byte faction) =>
            SystemAPI.HasComponent<BuildingTag>(entity) && !SystemAPI.IsComponentEnabled<Dead>(entity) &&
            SystemAPI.GetComponent<Faction>(entity).Value == faction;

        private void Sell(ref SystemState state, Entity building, DynamicBuffer<ResourceStock> stock, float refund)
        {
            var unfinished = ConstructionRules.IsUnderConstruction(state.EntityManager, building);
            if (SystemAPI.HasBuffer<ResourceCost>(building))
            {
                ResourceMath.Refund(stock, SystemAPI.GetBuffer<ResourceCost>(building), unfinished ? 1f : refund);
            }

            if (!SystemAPI.HasBuffer<ProductionQueueItem>(building))
            {
                return;
            }

            foreach (var item in SystemAPI.GetBuffer<ProductionQueueItem>(building))
            {
                if (SystemAPI.HasBuffer<ResourceCost>(item.Prefab))
                {
                    ResourceMath.Refund(stock, SystemAPI.GetBuffer<ResourceCost>(item.Prefab));
                }
            }
        }
    }
}
