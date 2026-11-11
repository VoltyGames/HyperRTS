using HyperRTS.Simulation.Navigation.Fields;
using HyperRTS.Simulation.Navigation.Grid;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;

namespace HyperRTS.Simulation.Navigation.Paths
{
    /// <summary>Builds the frame's new group fields, one per index.</summary>
    [BurstCompile]
    internal struct PathFieldJob : IJobParallelForDefer
    {
        [ReadOnly] public NavGrid Grid;

        // Read only as elements: each build writes its own field's cost list.
        [ReadOnly] public NativeArray<GroupField> Fields;
        [ReadOnly] public NativeArray<int> Builds;

        public void Execute(int index)
        {
            var field = Fields[Builds[index]];
            field.Field.Build(field.Access(Grid));
        }
    }
}
