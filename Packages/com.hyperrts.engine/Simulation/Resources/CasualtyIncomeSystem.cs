using HyperRTS.Core;
using HyperRTS.Simulation.Combat;
using HyperRTS.Simulation.Common;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace HyperRTS.Simulation.Resources
{
    /// <summary>
    /// Pays every owned, online <see cref="CasualtyIncome"/> its share of each unit that died this frame belonging to
    /// its owner or a hostile faction. Dead entities only exist for the frame they die in, so each pays once.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(LifecycleSystemGroup))]
    [UpdateAfter(typeof(DeathSystem))]
    public partial struct CasualtyIncomeSystem : ISystem
    {
        private EntityQuery _dying;
        private EntityQuery _earners;
        private BufferLookup<ResourceStock> _stockLookup;
        private ComponentLookup<PlayerStats> _statsLookup;
        private ComponentLookup<CasualtyIncome> _incomeLookup;
        private ComponentLookup<Faction> _factionLookup;
        private ComponentLookup<Offline> _offlineLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PlayerRegistry>();
            state.RequireForUpdate<FactionRelations>();
            state.RequireForUpdate<CasualtyIncome>();
            _dying = SystemAPI.QueryBuilder().WithAll<Dead, Faction, UnitTag, ResourceCost>().Build();
            _earners = SystemAPI.QueryBuilder().WithAll<CasualtyIncome, Faction>().Build();
            _stockLookup = state.GetBufferLookup<ResourceStock>();
            _statsLookup = state.GetComponentLookup<PlayerStats>();
            _incomeLookup = state.GetComponentLookup<CasualtyIncome>(true);
            _factionLookup = state.GetComponentLookup<Faction>(true);
            _offlineLookup = state.GetComponentLookup<Offline>(true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (_dying.IsEmpty)
            {
                return;
            }

            _stockLookup.Update(ref state);
            _statsLookup.Update(ref state);
            _incomeLookup.Update(ref state);
            _factionLookup.Update(ref state);
            _offlineLookup.Update(ref state);
            new CasualtyJob
            {
                Players = SystemAPI.GetSingleton<PlayerRegistry>(),
                Relations = SystemAPI.GetSingleton<FactionRelations>(),
                Earners = _earners.ToEntityArray(state.WorldUpdateAllocator),
                StockLookup = _stockLookup,
                StatsLookup = _statsLookup,
                IncomeLookup = _incomeLookup,
                FactionLookup = _factionLookup,
                OfflineLookup = _offlineLookup,
            }.Schedule(_dying);
        }

        /// <summary>Single-threaded: many deaths can pay one player.</summary>
        [BurstCompile]
        [WithAll(typeof(Dead), typeof(UnitTag))]
        private partial struct CasualtyJob : IJobEntity
        {
            public PlayerRegistry Players;
            public FactionRelations Relations;
            [ReadOnly] public NativeArray<Entity> Earners;
            public BufferLookup<ResourceStock> StockLookup;
            public ComponentLookup<PlayerStats> StatsLookup;
            [ReadOnly] public ComponentLookup<CasualtyIncome> IncomeLookup;
            [ReadOnly] public ComponentLookup<Faction> FactionLookup;
            [ReadOnly] public ComponentLookup<Offline> OfflineLookup;

            private void Execute(in Faction faction, in DynamicBuffer<ResourceCost> cost)
            {
                foreach (var earner in Earners)
                {
                    var owner = FactionLookup[earner].Value;
                    if (!Collects(earner, owner, faction.Value))
                    {
                        continue;
                    }

                    var income = IncomeLookup[earner];
                    var paid = (int)(CostOf(cost, income.Type) * income.Share);
                    ResourcePayout.Pay(ref StockLookup, ref StatsLookup, Players[owner], income.Type, paid);
                }
            }

            private bool Collects(Entity earner, byte owner, byte victim)
            {
                if (owner == Faction.Neutral || OfflineLookup.HasEnabled(earner))
                {
                    return false;
                }

                return victim == owner || Relations.IsHostile(owner, victim);
            }

            private static int CostOf(DynamicBuffer<ResourceCost> cost, UnityObjectRef<ResourceType> type)
            {
                foreach (var item in cost)
                {
                    if (item.Type.Equals(type))
                    {
                        return item.Amount;
                    }
                }

                return 0;
            }
        }
    }
}
