using HyperRTS.Core;
using HyperRTS.Simulation.Common;
using Unity.Burst;
using Unity.Entities;

namespace HyperRTS.Simulation.Resources
{
    /// <summary>Pays <see cref="ResourceIncome"/> to the owners of income entities; neutral ones restart their timer.</summary>
    [BurstCompile]
    [UpdateInGroup(typeof(ProductionSystemGroup))]
    public partial struct ResourceIncomeSystem : ISystem
    {
        private BufferLookup<ResourceStock> _stockLookup;
        private ComponentLookup<PlayerStats> _statsLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PlayerRegistry>();
            state.RequireForUpdate<ResourceIncome>();
            _stockLookup = state.GetBufferLookup<ResourceStock>();
            _statsLookup = state.GetComponentLookup<PlayerStats>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _stockLookup.Update(ref state);
            _statsLookup.Update(ref state);
            new IncomeJob
            {
                DeltaTime = SystemAPI.Time.DeltaTime,
                Players = SystemAPI.GetSingleton<PlayerRegistry>(),
                StockLookup = _stockLookup,
                StatsLookup = _statsLookup,
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

            private void Execute(ref ResourceIncome income, in Faction faction)
            {
                var player = faction.Value == Faction.Neutral ? Entity.Null : Players[faction.Value];
                if (!StockLookup.TryGetBuffer(player, out var stock))
                {
                    income.Elapsed = 0f;
                    return;
                }

                var paid = 0;
                if (!income.BonusPaid)
                {
                    income.BonusPaid = true;
                    paid += income.FirstOwnerBonus;
                }

                income.Elapsed += DeltaTime;
                while (income.Elapsed >= income.Interval)
                {
                    income.Elapsed -= income.Interval;
                    paid += income.Amount;
                }

                if (paid <= 0)
                {
                    return;
                }

                ResourceMath.Add(stock, income.Type, paid);
                if (StatsLookup.TryGetRefRW(player, out var stats))
                {
                    stats.ValueRW.ResourcesGathered += paid;
                }
            }
        }
    }
}
