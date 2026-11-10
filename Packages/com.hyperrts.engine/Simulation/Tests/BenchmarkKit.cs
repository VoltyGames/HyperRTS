using System;
using System.Linq;
using HyperRTS.Simulation.Navigation.Paths;
using Unity.Entities;
using Unity.PerformanceTesting;

namespace HyperRTS.Simulation.Tests
{
    /// <summary>Timing helpers for the <c>[Performance]</c> benchmarks, on top of <see cref="TestWorld"/>.</summary>
    public static class BenchmarkKit
    {
        public const int Warmup = 3;
        public const int Runs = 20;

        /// <summary>
        /// Times <paramref name="work"/> with the Performance Testing package, which records the profiler
        /// <paramref name="markers"/> too, and returns the median in milliseconds. <paramref name="prepare"/> and
        /// <paramref name="finish"/> run outside the timing before and after every run.
        /// </summary>
        public static double Median(string name, Action work, Action prepare = null, Action finish = null,
            params string[] markers)
        {
            var group = new SampleGroup(name);
            Measure.Method(work).SampleGroup(group).ProfilerMarkers(markers).SetUp(prepare).CleanUp(finish)
                .WarmupCount(Warmup).MeasurementCount(Runs).Run();
            var samples = group.Samples.OrderBy(sample => sample).ToArray();
            return (samples[(samples.Length - 1) / 2] + samples[samples.Length / 2]) / 2.0;
        }

        /// <summary>The pathfinder's running totals, with its jobs finished.</summary>
        public static PathStats ReadPathStats(this TestWorld world)
        {
            world.EntityManager.CompleteAllTrackedJobs();
            var handle = world.World.GetExistingSystem<PathfindingSystem>();
            return world.World.Unmanaged.GetUnsafeSystemRef<PathfindingSystem>(handle).Stats;
        }

        /// <summary>Updates one system and waits for its jobs.</summary>
        public static void UpdateSystem<T>(this TestWorld world) where T : unmanaged, ISystem
        {
            world.World.GetExistingSystem<T>().Update(world.World.Unmanaged);
            world.EntityManager.CompleteAllTrackedJobs();
        }
    }
}
