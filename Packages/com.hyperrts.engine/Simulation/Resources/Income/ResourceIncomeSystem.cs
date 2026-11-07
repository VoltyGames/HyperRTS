using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Common.Players;
using HyperRTS.Simulation.Transport;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Resources.Income
{
    /// <summary>
    /// Pays <see cref="ResourceIncome"/> to the owners of income entities: neutral ones restart their timer, offline ones pause it.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(ProductionSystemGroup))]
    public partial struct ResourceIncomeSystem : ISystem
    {

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PlayerRegistry>();
            state.RequireForUpdate<ResourceIncome>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            new IncomeJob
            {
                DeltaTime = SystemAPI.Time.DeltaTime,
                Players = SystemAPI.GetSingleton<PlayerRegistry>(),
                StockLookup = SystemAPI.GetBufferLookup<ResourceStock>(false),
                StatsLookup = SystemAPI.GetComponentLookup<PlayerStats>(false),
                OfflineLookup = SystemAPI.GetComponentLookup<Offline>(true),
                OccupantLookup = SystemAPI.GetComponentLookup<OccupantIncome>(true),
                CargoLookup = SystemAPI.GetBufferLookup<Cargo>(true),
                ReserveLookup = SystemAPI.GetComponentLookup<ResourceReserve>(false),
            }.Schedule();
        }

        /// <summary>Single-threaded: several income entities pay into one player's stock.</summary>
        [BurstCompile]
        private partial struct IncomeJob : IJobEntity
        {
            public float DeltaTime;
            public PlayerRegistry Players;
            public BufferLookup<ResourceStock> StockLookup;
            public ComponentLookup<PlayerStats> StatsLookup;
            [ReadOnly] public ComponentLookup<Offline> OfflineLookup;
            [ReadOnly] public ComponentLookup<OccupantIncome> OccupantLookup;
            [ReadOnly] public BufferLookup<Cargo> CargoLookup;
            public ComponentLookup<ResourceReserve> ReserveLookup;

            private void Execute(Entity entity, ref ResourceIncome income, in Owner owner)
            {
                var player = owner.Value == Owner.Neutral ? Entity.Null : Players[owner.Value];
                if (!StockLookup.HasBuffer(player))
                {
                    income.Elapsed = 0f;
                    return;
                }

                if (OfflineLookup.HasEnabled(entity))
                {
                    return;
                }

                var due = IntervalTimer.Advance(ref income.Elapsed, income.Interval, DeltaTime);
                var earned = due > 0 ? Draw(entity, due * PerInterval(entity, income)) : 0;
                ResourcePayout.Pay(ref StockLookup, ref StatsLookup, player, income.Type, TakeBonus(ref income) + earned);
            }

            private static int TakeBonus(ref ResourceIncome income)
            {
                if (income.BonusPaid)
                {
                    return 0;
                }

                income.BonusPaid = true;
                return income.FirstOwnerBonus;
            }

            /// <summary>An occupant-paid income without a cargo hold is bad data, so the lookup throws.</summary>
            private int PerInterval(Entity entity, in ResourceIncome income) =>
                OccupantLookup.HasComponent(entity) ? income.Amount * CargoLookup[entity].Length : income.Amount;

            private int Draw(Entity entity, int amount)
            {
                if (amount <= 0 || !ReserveLookup.TryGetRefRW(entity, out var reserve))
                {
                    return amount;
                }

                var drawn = math.min(amount, reserve.ValueRO.Remaining);
                reserve.ValueRW.Remaining -= drawn;
                return drawn;
            }
        }
    }
}
