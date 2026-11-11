using HyperRTS.Simulation.Navigation.Fields;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;

namespace HyperRTS.Simulation.Navigation.Grid
{
    /// <summary>Marks the field sectors round scenery that changed, then keeps this fill to compare with.</summary>
    [BurstCompile]
    internal struct NavSceneryDiffJob : IJob
    {
        public NavField Field;
        [ReadOnly] public NativeArray<byte> Scenery;
        public NativeArray<byte> Previous;

        public void Execute() => Field.MarkChanges(Scenery, Previous);
    }
}
