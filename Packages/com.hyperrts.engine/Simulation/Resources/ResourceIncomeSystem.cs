using HyperRTS.Core;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Transport;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Resources
{
    /// <summary>
    /// Pays <see cref="ResourceIncome"/> to the owners of income entities: neutral ones restart their timer, offline
    /// ones pause it. <see cref="OccupantIncome"/> scales a payment by passengers and <see cref="ResourceReserve"/>
    /// caps it at what is left.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(ProductionSystemGroup))]
    public partial struct ResourceIncomeSystem : ISystem
    {
        private BufferLookup<ResourceStock> _stockLookup;
        private ComponentLookup<PlayerStats> _statsLookup;
        private ComponentLookup<Offline> _offlineLookup;
        private ComponentLookup<OccupantIncome> _occupantLookup;
        private BufferLookup<Cargo> _cargoLookup;
        private ComponentLookup<ResourceReserve> _reserveLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PlayerRegistry>();
            state.RequireForUpdate<ResourceIncome>();
            _stockLookup = state.GetBufferLookup<ResourceStock>();
            _statsLookup = state.GetComponentLookup<PlayerStats>();
            _offlineLookup = state.GetComponentLookup<Offline>(true);
            _occupantLookup = state.GetComponentLookup<OccupantIncome>(true);
            _cargoLookup = state.GetBufferLookup<Cargo>(true);
            _reserveLookup = state.GetComponentLookup<ResourceReserve>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _stockLookup.Update(ref state);
            _statsLookup.Update(ref state);
            _offlineLookup.Update(ref state);
            _occupantLookup.Update(ref state);
            _cargoLookup.Update(ref state);
            _reserveLookup.Update(ref state);
            new IncomeJob
            {
                DeltaTime = SystemAPI.Time.DeltaTime,
                Players = SystemAPI.GetSingleton<PlayerRegistry>(),
                StockLookup = _stockLookup,
                StatsLookup = _statsLookup,
                OfflineLookup = _offlineLookup,
                OccupantLookup = _occupantLookup,
                CargoLookup = _cargoLookup,
                ReserveLookup = _reserveLookup,
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

            private void Execute(Entity entity, ref ResourceIncome income, in Faction faction)
            {
                var player = faction.Value == Faction.Neutral ? Entity.Null : Players[faction.Value];
                if (!StockLookup.HasBuffer(player))
                {
                    income.Elapsed = 0f;
                    return;
                }

                if (OfflineLookup.HasEnabled(entity))
                {
                    return;
                }

                var paid = TakeBonus(ref income) + Draw(entity, DueIntervals(ref income) * PerInterval(entity, income));
                ResourcePayout.Pay(ref StockLookup, ref StatsLookup, player, income.Type, paid);
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

            private int DueIntervals(ref ResourceIncome income)
            {
                income.Elapsed += DeltaTime;
                var due = 0;
                while (income.Elapsed >= income.Interval)
                {
                    income.Elapsed -= income.Interval;
                    due++;
                }

                return due;
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
