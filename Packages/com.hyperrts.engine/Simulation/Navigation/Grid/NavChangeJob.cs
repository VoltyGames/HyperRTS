using Unity.Burst;
using Unity.Jobs;

namespace HyperRTS.Simulation.Navigation.Grid
{
    /// <summary>Records the box this grid version changed and marks the field sectors round it.</summary>
    [BurstCompile]
    internal struct NavChangeJob : IJob
    {
        public NavGrid Grid;

        public void Execute() => Grid.RecordChanges();
    }
}
