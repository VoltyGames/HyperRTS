using HyperRTS.Core;
using HyperRTS.Simulation.Common;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace HyperRTS.Simulation.Buildings
{
    /// <summary>
    /// Sets <see cref="OfflineReason.UnderConstruction"/> from <see cref="ConstructionProgress"/> next to the reasons
    /// power and games set, and enables <see cref="Offline"/> while any reason holds. Runs after power so combat sees
    /// this frame's state.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(OrderSystemGroup), OrderLast = true)]
    [UpdateAfter(typeof(PowerSystem))]
    public partial struct OfflineSystem : ISystem
    {
        private ComponentLookup<ConstructionProgress> _sites;

        [BurstCompile]
        public void OnCreate(ref SystemState state) => _sites = state.GetComponentLookup<ConstructionProgress>(true);

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _sites.Update(ref state);
            new UpdateJob { Sites = _sites }.ScheduleParallel();
        }

        [BurstCompile]
        [WithPresent(typeof(Offline))]
        private partial struct UpdateJob : IJobEntity
        {
            [ReadOnly] public ComponentLookup<ConstructionProgress> Sites;

            private void Execute(Entity entity, ref Offline offline, EnabledRefRW<Offline> isOffline)
            {
                offline.Set(OfflineReason.UnderConstruction, Sites.HasEnabled(entity));
                var off = offline.Reasons != OfflineReason.None;
                if (isOffline.ValueRO != off)
                {
                    isOffline.ValueRW = off;
                }
            }
        }
    }
}
