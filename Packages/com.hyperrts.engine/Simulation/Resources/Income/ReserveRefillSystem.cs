using HyperRTS.Simulation.Common;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Resources.Income
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

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<ReserveRefill>();
            _reserves = SystemAPI.QueryBuilder().WithAll<ResourceReserve, ResourceIncome, Faction>().Build();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            new RefillJob
            {
                DeltaTime = SystemAPI.Time.DeltaTime,
                Reserves = _reserves.ToEntityArray(state.WorldUpdateAllocator),
                ReserveLookup = SystemAPI.GetComponentLookup<ResourceReserve>(false),
                IncomeLookup = SystemAPI.GetComponentLookup<ResourceIncome>(true),
                FactionLookup = SystemAPI.GetComponentLookup<Faction>(true),
                OfflineLookup = SystemAPI.GetComponentLookup<Offline>(true),
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
                var offline = OfflineLookup.HasEnabled(entity);
                var due = IntervalTimer.Due(ref refill.Elapsed, refill.Interval, DeltaTime, faction.Value, offline);
                if (due > 0)
                {
                    TopUp(faction.Value, refill.Type, due * refill.Amount);
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
