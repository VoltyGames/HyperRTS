using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;

namespace HyperRTS.Simulation.Vision
{
    /// <summary>Zeroes a byte grid before it is restamped.</summary>
    [BurstCompile]
    internal struct ClearBytesJob : IJob
    {
        public NativeArray<byte> Cells;

        public void Execute()
        {
            for (var i = 0; i < Cells.Length; i++)
            {
                Cells[i] = 0;
            }
        }
    }
}
