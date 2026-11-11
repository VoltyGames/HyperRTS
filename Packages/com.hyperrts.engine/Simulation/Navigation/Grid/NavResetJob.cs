using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;

namespace HyperRTS.Simulation.Navigation.Grid
{
    /// <summary>Keeps the last stamp to compare with, then starts again from the ground.</summary>
    [BurstCompile]
    internal struct NavResetJob : IJob
    {
        [ReadOnly] public NativeArray<byte> Base;
        public NativeArray<byte> Cells;
        public NativeArray<byte> Previous;

        public void Execute()
        {
            Previous.CopyFrom(Cells);
            Cells.CopyFrom(Base);
        }
    }
}
