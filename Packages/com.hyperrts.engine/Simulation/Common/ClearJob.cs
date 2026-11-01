using HyperRTS.Simulation.Common;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;

[assembly: RegisterGenericJobType(typeof(ClearJob<byte>))]
[assembly: RegisterGenericJobType(typeof(ClearJob<ulong>))]
[assembly: RegisterGenericJobType(typeof(ClearJob<Entity>))]

namespace HyperRTS.Simulation.Common
{
    /// <summary>Sets every element of an array to its default; Burst needs each element type registered above.</summary>
    [BurstCompile]
    public struct ClearJob<T> : IJobParallelFor where T : unmanaged
    {
        private const int Batch = 4096;

        public NativeArray<T> Values;

        public void Execute(int index) => Values[index] = default;

        public static JobHandle Schedule(NativeArray<T> values, JobHandle dependency) =>
            new ClearJob<T> { Values = values }.Schedule(values.Length, Batch, dependency);
    }
}
