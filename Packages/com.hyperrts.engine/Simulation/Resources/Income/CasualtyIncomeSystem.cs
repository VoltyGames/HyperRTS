using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Common.Players;
using HyperRTS.Simulation.Death;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace HyperRTS.Simulation.Resources.Income
{
    /// <summary>
    /// Pays every owned, online <see cref="CasualtyIncome"/> its share of each unit that died this frame belonging to
    /// its owner or a hostile owner. Dead entities only exist for the frame they die in, so each pays once.
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
            state.RequireForUpdate<OwnerRelations>();
            state.RequireForUpdate<CasualtyIncome>();
            _dying = SystemAPI.QueryBuilder().WithAll<Dead, Owner, UnitTag, ResourceCost>().Build();
            _earners = SystemAPI.QueryBuilder().WithAll<CasualtyIncome, Owner>().Build();
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
                Relations = SystemAPI.GetSingleton<OwnerRelations>(),
                Earners = _earners.ToEntityArray(state.WorldUpdateAllocator),
                StockLookup = SystemAPI.GetBufferLookup<ResourceStock>(false),
                StatsLookup = SystemAPI.GetComponentLookup<PlayerStats>(false),
                IncomeLookup = SystemAPI.GetComponentLookup<CasualtyIncome>(true),
                OwnerLookup = SystemAPI.GetComponentLookup<Owner>(true),
                OfflineLookup = SystemAPI.GetComponentLookup<Offline>(true),
            }.Schedule(_dying);
        }

        /// <summary>Single-threaded: many deaths can pay one player.</summary>
        [BurstCompile]
        private partial struct CasualtyJob : IJobEntity
        {
            public PlayerRegistry Players;
            public OwnerRelations Relations;
            [ReadOnly] public NativeArray<Entity> Earners;
            public BufferLookup<ResourceStock> StockLookup;
            public ComponentLookup<PlayerStats> StatsLookup;
            [ReadOnly] public ComponentLookup<CasualtyIncome> IncomeLookup;
            [ReadOnly] public ComponentLookup<Owner> OwnerLookup;
            [ReadOnly] public ComponentLookup<Offline> OfflineLookup;

            private void Execute(in Owner owner, in DynamicBuffer<ResourceCost> cost)
            {
                foreach (var earner in Earners)
                {
                    var earnerOwner = OwnerLookup[earner].Value;
                    if (!Collects(earner, earnerOwner, owner.Value))
                    {
                        continue;
                    }

                    var income = IncomeLookup[earner];
                    var paid = (int)(GetCost(cost, income.Type) * income.Share);
                    ResourcePayout.Pay(ref StockLookup, ref StatsLookup, Players[earnerOwner], income.Type, paid);
                }
            }

            private bool Collects(Entity earner, byte owner, byte victim)
            {
                if (owner == Owner.Neutral || OfflineLookup.HasEnabled(earner))
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
