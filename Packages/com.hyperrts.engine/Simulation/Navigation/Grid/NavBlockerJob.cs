using HyperRTS.Simulation.Navigation.Areas;
using Unity.Burst;
using Unity.Entities;
using Unity.Transforms;

namespace HyperRTS.Simulation.Navigation.Grid
{
    /// <summary>Single-threaded: blockers may share cells. Disabled ones are skipped.</summary>
    [BurstCompile]
    internal partial struct NavBlockerJob : IJobEntity
    {
        public NavGrid Grid;

        private void Execute(in Blocker blocker, in LocalTransform transform)
        {
            using var marker = NavGridSystem.RestampMarker.Auto();
            blocker.Stamp(ref Grid, transform);
        }
    }
}
