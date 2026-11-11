using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;

namespace HyperRTS.Simulation.Navigation.Grid
{
    /// <summary>Keeps the cells as they stand before obstacles close them, for placement.</summary>
    [BurstCompile]
    internal struct NavUnbuiltJob : IJob
    {
        [ReadOnly] public NativeArray<byte> Cells;
        public NativeArray<byte> Unbuilt;

        public void Execute() => Unbuilt.CopyFrom(Cells);
    }
}
