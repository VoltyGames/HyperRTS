using HyperRTS.Simulation.Combat.Death;
using HyperRTS.Simulation.Common;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace HyperRTS.Simulation.Resources.Income
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

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PlayerRegistry>();
            state.RequireForUpdate<FactionRelations>();
            state.RequireForUpdate<CasualtyIncome>();
            _dying = SystemAPI.QueryBuilder().WithAll<Dead, Faction, UnitTag, ResourceCost>().Build();
            _earners = SystemAPI.QueryBuilder().WithAll<CasualtyIncome, Faction>().Build();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (_dying.IsEmpty)
            {
                return;
            }

            new CasualtyJob
            {
                Players = SystemAPI.GetSingleton<PlayerRegistry>(),
                Relations = SystemAPI.GetSingleton<FactionRelations>(),
                Earners = _earners.ToEntityArray(state.WorldUpdateAllocator),
                StockLookup = SystemAPI.GetBufferLookup<ResourceStock>(false),
                StatsLookup = SystemAPI.GetComponentLookup<PlayerStats>(false),
                IncomeLookup = SystemAPI.GetComponentLookup<CasualtyIncome>(true),
                FactionLookup = SystemAPI.GetComponentLookup<Faction>(true),
                OfflineLookup = SystemAPI.GetComponentLookup<Offline>(true),
            }.Schedule(_dying);
        }

        /// <summary>Single-threaded: many deaths can pay one player.</summary>
        [BurstCompile]
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
                    var paid = (int)(GetCost(cost, income.Type) * income.Share);
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

            private static int GetCost(DynamicBuffer<ResourceCost> cost, UnityObjectRef<ResourceType> type)
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
