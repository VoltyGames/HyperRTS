using HyperRTS.Core;
using HyperRTS.Simulation.Common;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Resources
{
    /// <summary>
    /// Adds each owned, online <see cref="ReserveRefill"/>'s amount to its owner's reserves of that resource every
    /// interval, capped at capacity. Neutral refills restart their timer, offline ones pause it.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(ProductionSystemGroup))]
    [UpdateBefore(typeof(ResourceIncomeSystem))]
    public partial struct ReserveRefillSystem : ISystem
    {
        private EntityQuery _reserves;
        private ComponentLookup<ResourceReserve> _reserveLookup;
        private ComponentLookup<ResourceIncome> _incomeLookup;
        private ComponentLookup<Faction> _factionLookup;
        private ComponentLookup<Offline> _offlineLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<ReserveRefill>();
            _reserves = SystemAPI.QueryBuilder().WithAll<ResourceReserve, ResourceIncome, Faction>().Build();
            _reserveLookup = state.GetComponentLookup<ResourceReserve>();
            _incomeLookup = state.GetComponentLookup<ResourceIncome>(true);
            _factionLookup = state.GetComponentLookup<Faction>(true);
            _offlineLookup = state.GetComponentLookup<Offline>(true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _reserveLookup.Update(ref state);
            _incomeLookup.Update(ref state);
            _factionLookup.Update(ref state);
            _offlineLookup.Update(ref state);
            new RefillJob
            {
                DeltaTime = SystemAPI.Time.DeltaTime,
                Reserves = _reserves.ToEntityArray(state.WorldUpdateAllocator),
                ReserveLookup = _reserveLookup,
                IncomeLookup = _incomeLookup,
                FactionLookup = _factionLookup,
                OfflineLookup = _offlineLookup,
            }.Schedule();
        }

        /// <summary>Single-threaded: several refills can top up one reserve.</summary>
        [BurstCompile]
        private partial struct RefillJob : IJobEntity
        {
            public float DeltaTime;
            [ReadOnly] public NativeArray<Entity> Reserves;
            public ComponentLookup<ResourceReserve> ReserveLookup;
            [ReadOnly] public ComponentLookup<ResourceIncome> IncomeLookup;
            [ReadOnly] public ComponentLookup<Faction> FactionLookup;
            [ReadOnly] public ComponentLookup<Offline> OfflineLookup;

            private void Execute(Entity entity, ref ReserveRefill refill, in Faction faction)
            {
                var owner = faction.Value;
                if (owner == Faction.Neutral)
                {
                    refill.Elapsed = 0f;
                    return;
                }

                if (OfflineLookup.HasEnabled(entity))
                {
                    return;
                }

                refill.Elapsed += DeltaTime;
                var due = 0;
                while (refill.Elapsed >= refill.Interval)
                {
                    refill.Elapsed -= refill.Interval;
                    due++;
                }

                if (due > 0)
                {
                    TopUp(owner, refill.Type, due * refill.Amount);
                }
            }

            private void TopUp(byte owner, UnityObjectRef<ResourceType> type, int amount)
            {
                foreach (var target in Reserves)
                {
                    var sameOwner = FactionLookup[target].Value == owner;
                    if (!sameOwner || !IncomeLookup[target].Type.Equals(type))
                    {
                        continue;
                    }

                    ref var reserve = ref ReserveLookup.GetRefRW(target).ValueRW;
                    reserve.Remaining = math.min(reserve.Capacity, reserve.Remaining + amount);
                }
            }
        }
    }
}
