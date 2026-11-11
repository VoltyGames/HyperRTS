using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;

namespace HyperRTS.Simulation.Vision.Sight
{
    /// <summary>Clears every sight cell's blockers before this frame's are stamped.</summary>
    [BurstCompile]
    internal struct SightClearBlocksJob : IJobParallelFor
    {
        public NativeArray<SightCell> Cells;

        public void Execute(int index)
        {
            var cell = Cells[index];
            cell.ClearBlocks();
            Cells[index] = cell;
        }
    }
}
