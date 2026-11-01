using HyperRTS.Simulation.Navigation.Spatial;
using NUnit.Framework;
using Unity.PerformanceTesting;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Tests.Navigation
{
    public partial class NavBenchmarkTests
    {
        private const float CrowdQueryRadius = 12f;

        [Test, Performance]
        public void SpatialIndex_ThousandUnits()
        {
            var random = new Random(31);
            var points = new NativeArray<float3>(1000, Allocator.Persistent);
            for (var i = 0; i < points.Length; i++)
            {
                // Packed into a quarter of the map, as armies bunch, so cells hold several units each.
                points[i] = new float3(random.NextFloat(-290f, -140f), 0f, random.NextFloat(-150f, 150f));
                _world.SpawnUnit((byte)(1 + i % 2), points[i]);
            }

            _world.Tick();
            var found = new NativeArray<int>(points.Length, Allocator.Persistent);
            using var query = _world.EntityManager.CreateEntityQuery(typeof(SpatialIndex));
            var median = BenchmarkKit.Median("SpatialFrame", () =>
            {
                _world.UpdateSystem<SpatialIndexSystem>();
                var index = query.GetSingleton<SpatialIndex>();
                new QueryJob { Index = index, Points = points, Found = found }.Schedule(points.Length, 32).Complete();
            }, null, null, "SpatialIndexSystem.Index", "SpatialIndexSystem.Sort");

            var mean = 0.0;
            foreach (var count in found)
            {
                mean += count;
            }

            points.Dispose();
            found.Dispose();
            TestContext.WriteLine($"1000-unit index and queries: median {median:F3} ms, {mean / 1000.0:F1} found each");
            Assert.Less(median, 2.0, $"median {median:F3} ms, under twice 1 ms");
        }

        [BurstCompile]
        private struct QueryJob : IJobParallelFor
        {
            [ReadOnly] public SpatialIndex Index;
            [ReadOnly] public NativeArray<float3> Points;
            public NativeArray<int> Found;

            public void Execute(int i)
            {
                var counter = new Counter();
                Index.QueryHulls(Points[i], CrowdQueryRadius, ref counter);
                Found[i] = counter.Count;
            }
        }

        private struct Counter : ISpatialVisitor
        {
            public int Count;

            public void Visit(in SpatialEntry entry) => Count++;
        }
    }
}
