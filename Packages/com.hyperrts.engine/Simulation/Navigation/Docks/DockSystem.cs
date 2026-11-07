using HyperRTS.Simulation.Common;
using Unity.Burst;
using Unity.Entities;

namespace HyperRTS.Simulation.Navigation.Docks
{
    /// <summary>
    /// Frees dock bays their occupant didn't hold since the last run, as when it died or its order moved on, and
    /// rolls each bay's line over to the new frame.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(ProductionSystemGroup))]
    public partial struct DockSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state) =>
            state.RequireForUpdate(SystemAPI.QueryBuilder().WithAll<DockBay>().Build());

        [BurstCompile]
        public void OnUpdate(ref SystemState state) => new FreeJob().ScheduleParallel();

        [BurstCompile]
        private partial struct FreeJob : IJobEntity
        {
            private void Execute(DynamicBuffer<DockBay> bays)
            {
                for (var i = 0; i < bays.Length; i++)
                {
                    ref var bay = ref bays.ElementAt(i);
                    bay.Reservation.Expire();
                    bay.Line = bay.NextLine;
                    bay.NextLine = 0u;
                }
            }
        }
    }
}
