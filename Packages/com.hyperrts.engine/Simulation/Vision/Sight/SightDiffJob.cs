using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;

namespace HyperRTS.Simulation.Vision.Sight
{
    /// <summary>Marks the tiles whose sight the restamp changed, so only viewers near them draw again.</summary>
    [BurstCompile]
    internal struct SightDiffJob : IJob
    {
        public SightBlockers Blockers;
        [ReadOnly] public NativeArray<SightCell> Previous;

        public void Execute()
        {
            for (var i = 0; i < Previous.Length; i++)
            {
                if (Blockers.Cells[i].BlocksUnlike(Previous[i]))
                {
                    Blockers.MarkDirty(Blockers.Grid.Cell(i));
                }
            }
        }
    }
}
