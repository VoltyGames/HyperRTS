using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;

namespace HyperRTS.Simulation.Vision
{
    /// <summary>Zeroes a fog grid before it is restamped.</summary>
    [BurstCompile]
    internal struct ClearCellsJob : IJob
    {
        public NativeArray<ushort> Cells;

        public void Execute()
        {
            for (var i = 0; i < Cells.Length; i++)
            {
                Cells[i] = 0;
            }
        }
    }
}
