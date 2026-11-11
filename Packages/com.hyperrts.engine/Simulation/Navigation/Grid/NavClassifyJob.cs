using HyperRTS.Simulation.Navigation.Fields;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;

namespace HyperRTS.Simulation.Navigation.Grid
{
    /// <summary>Classifies each cell's ground surface from the terrain, flooded or not.</summary>
    [BurstCompile]
    internal struct NavClassifyJob : IJobParallelFor
    {
        [ReadOnly] public NavGrid Grid;
        public NativeArray<byte> Base;
        public bool Flood;
        public float MaxGradient;

        public void Execute(int index) => Base[index] = (byte)Grid.GroundSurface(index, Flood, MaxGradient);
    }
}
