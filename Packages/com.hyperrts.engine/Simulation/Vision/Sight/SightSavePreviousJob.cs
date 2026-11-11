using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;

namespace HyperRTS.Simulation.Vision.Sight
{
    /// <summary>Keeps the last stamp to compare with before blocks stamp again.</summary>
    [BurstCompile]
    internal struct SightSavePreviousJob : IJob
    {
        [ReadOnly] public SightBlockers Blockers;
        public NativeArray<SightCell> Previous;

        public void Execute() => Previous.CopyFrom(Blockers.Cells);
    }
}
