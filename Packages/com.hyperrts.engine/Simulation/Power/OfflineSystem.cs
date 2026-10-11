using HyperRTS.Core;
using HyperRTS.Simulation.Common;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace HyperRTS.Simulation.Power
{
    /// <summary>
    /// Sets the engine's <see cref="OfflineReason"/>s (under construction, unpowered) next to whatever reasons games
    /// set, and enables <see cref="Offline"/> while any reason holds. Runs after power so combat sees this frame's state.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(OrderSystemGroup), OrderLast = true)]
    [UpdateAfter(typeof(PowerSystem))]
    public partial struct OfflineSystem : ISystem
    {
        private ComponentLookup<ConstructionProgress> _sites;
        private ComponentLookup<Unpowered> _unpowered;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _sites = state.GetComponentLookup<ConstructionProgress>(true);
            _unpowered = state.GetComponentLookup<Unpowered>(true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _sites.Update(ref state);
            _unpowered.Update(ref state);
            new UpdateJob { Sites = _sites, Unpowered = _unpowered }.ScheduleParallel();
        }

        [BurstCompile]
        [WithPresent(typeof(Offline))]
        private partial struct UpdateJob : IJobEntity
        {
            [ReadOnly] public ComponentLookup<ConstructionProgress> Sites;
            [ReadOnly] public ComponentLookup<Unpowered> Unpowered;

            private void Execute(Entity entity, ref Offline offline, EnabledRefRW<Offline> isOffline)
            {
                var reasons = offline.Reasons & ~OfflineReason.Engine;
                if (Sites.HasEnabled(entity))
                {
                    reasons |= OfflineReason.UnderConstruction;
                }

                if (Unpowered.HasEnabled(entity))
                {
                    reasons |= OfflineReason.Unpowered;
                }

                offline.Reasons = reasons;
                var off = reasons != OfflineReason.None;
                if (isOffline.ValueRO != off)
                {
                    isOffline.ValueRW = off;
                }
            }
        }
    }
}
