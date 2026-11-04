using HyperRTS.Simulation.Vision;
using HyperRTS.Simulation.Vision.Fog;
using HyperRTS.Simulation.Vision.Sight;
using NUnit.Framework;
using Unity.PerformanceTesting;
using Unity.Core;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.Tests.Vision
{
    /// <summary>
    /// Vision timings on a 600 m map at 2 m fog cells with hills, a village and woods, against twice the plan's budgets.
    /// Each case warms up first, so Burst is compiled, and asserts the median.
    /// </summary>
    [Category("Benchmark")]
    public class VisionBenchmarkTests
    {
        private const float MapSize = 600f;
        private const int Rounds = 20;

        private TestWorld _world;
        private double _time = 1000.0;

        [SetUp]
        public void SetUp()
        {
            _world = new TestWorld();
            _world.CreateMatch(new byte[] { 1, 2 }, MapSize);
            _world.CreateTerrain(p => 6f * math.sin(p.x * 0.03f) * math.cos(p.y * 0.025f), 2f);
            var random = new Random(31);
            for (var i = 0; i < 40; i++)
            {
                var place = new float3(random.NextFloat(-60f, 60f), 0f, random.NextFloat(-60f, 60f));
                _world.SpawnSightBlock(place, new float2(10f, 8f), 6f);
            }

            for (var i = 0; i < 400; i++)
            {
                var place = new float3(random.NextFloat(100f, 200f), 0f, random.NextFloat(-150f, 150f));
                _world.SpawnSightBlock(place, new SightBlock { Size = new float2(8f, 8f), Density = 0.1f, PorousTop = 15f });
            }

            _world.Tick();
        }

        [TearDown]
        public void TearDown() => _world.Dispose();

        [Test, Performance]
        public void EightHundredASide_HalfMoving()
        {
            var units = Viewers(800, new float2(-280f, -280f), new float2(280f, 280f));
            var stamp = MeasureStamp("HalfMoving", () => Shift(units, 0, units.Length / 2));

            TestContext.WriteLine($"1,600 viewers, 800 moving: stamp median {stamp:F3} ms");
            Assert.Less(stamp, 2.4, $"median {stamp:F3} ms, under twice the 1.2 ms of drawing and merging");
        }

        [Test, Performance]
        public void VillageCollapsing_OneSectionASecond()
        {
            Viewers(800, new float2(-280f, -280f), new float2(280f, 280f));
            var houses = new Entity[BenchmarkKit.Warmup + BenchmarkKit.Runs];
            for (var i = 0; i < houses.Length; i++)
            {
                houses[i] = _world.SpawnSightBlock(new float3(-40f + i * 4f, 0f, -100f), new float2(4f, 4f), 6f);
            }

            Run();
            var next = 0;
            var median = BenchmarkKit.Median("Collapse",
                () =>
                {
                    _world.UpdateSystem<SightBlockSystem>();
                    _world.UpdateSystem<FogOfWarSystem>();
                },
                () =>
                {
                    _world.SetEnabled<SightBlock>(houses[next++], false);
                    AdvanceTime();
                }, null, "SightBlockSystem.Stamp", "SightAreaJob.Draw", "FogMergeJob.Merge");

            TestContext.WriteLine($"a section a second: restamp and fog median {median:F3} ms");
            Assert.Less(median, 2.6, $"median {median:F3} ms, under twice 0.1 ms of blockers and 1.2 ms of fog");
        }

        [Test, Performance]
        public void ForestEdge_TwoHundredViewers()
        {
            var units = Viewers(100, new float2(80f, -150f), new float2(110f, 150f));
            var stamp = MeasureStamp("ForestEdge", () => Shift(units, 0, units.Length));

            TestContext.WriteLine($"200 viewers at a forest edge, all moving: stamp median {stamp:F3} ms");
            Assert.Less(stamp, 2.4, $"median {stamp:F3} ms, under twice 1.2 ms");
        }

        /// <summary>Spawns this many viewers a side between the corners, 35 m of sight each.</summary>
        private Entity[] Viewers(int perSide, float2 min, float2 max)
        {
            var random = new Random(7);
            var units = new Entity[perSide * 2];
            for (var i = 0; i < units.Length; i++)
            {
                var place = random.NextFloat2(min, max);
                units[i] = _world.SpawnUnit((byte)(1 + i % 2), new float3(place.x, 0f, place.y));
                _world.Set(units[i], new VisionRange { Value = 35f, EyeHeight = 2f });
            }

            return units;
        }

        /// <summary>
        /// Moves units east at 7.5 m/s, a brisk vehicle: 0.75 m a stamp, so about a third of them cross into another
        /// fog cell each stamp, as the plan's budget assumes.
        /// </summary>
        private void Shift(Entity[] units, int from, int count)
        {
            const float step = 0.75f;
            for (var i = from; i < from + count; i++)
            {
                var transform = _world.Get<LocalTransform>(units[i]);
                transform.Position.x += step;
                _world.Set(units[i], transform);
            }
        }

        /// <summary>Median fog stamp in milliseconds after each change.</summary>
        private double MeasureStamp(string name, System.Action change)
        {
            Run();
            return BenchmarkKit.Median(name, _world.UpdateSystem<FogOfWarSystem>, () =>
            {
                change();
                AdvanceTime();
            }, null, "SightAreaJob.Draw", "FogMergeJob.Merge");
        }

        private void Run()
        {
            AdvanceTime();
            _world.UpdateSystem<SightBlockSystem>();
            _world.UpdateSystem<FogOfWarSystem>();
        }

        /// <summary>Moves the clock past the fog's interval and waits for jobs, so the next update stamps.</summary>
        private void AdvanceTime()
        {
            _time += FogOfWarSystem.UpdateInterval * 2f;
            _world.World.SetTime(new TimeData(_time, FogOfWarSystem.UpdateInterval));
            _world.EntityManager.CompleteAllTrackedJobs();
        }
    }
}
