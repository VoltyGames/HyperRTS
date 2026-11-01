using System.Diagnostics;
using HyperRTS.Simulation.Vision;
using NUnit.Framework;
using Unity.Core;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.Tests.Vision
{
    /// <summary>
    /// Vision timings on a 600 m map at 2 m fog cells with hills, a village and woods, against twice the plan's budgets.
    /// Each case runs once untimed first, so Burst is warm.
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

        [Test]
        public void EightHundredASide_HalfMoving()
        {
            var units = Viewers(800, new float2(-280f, -280f), new float2(280f, 280f));
            var stamp = Measure(round => Shift(units, 0, units.Length / 2, round));

            TestContext.WriteLine($"1,600 viewers, 800 moving: stamp mean {stamp.x:F3} ms, max {stamp.y:F3} ms");
            Assert.Less(stamp.x, 2.4, $"mean {stamp.x:F3} ms, under twice the 1.2 ms of drawing and merging");
        }

        [Test]
        public void VillageCollapsing_OneSectionASecond()
        {
            Viewers(800, new float2(-280f, -280f), new float2(280f, 280f));
            var houses = new Entity[Rounds + 1];
            for (var i = 0; i < houses.Length; i++)
            {
                houses[i] = _world.SpawnSightBlock(new float3(-40f + i * 4f, 0f, -100f), new float2(4f, 4f), 6f);
            }

            Run();
            double total = 0.0, slowest = 0.0;
            for (var i = -1; i < Rounds; i++)
            {
                _world.EntityManager.SetComponentEnabled<SightBlock>(houses[i + 1], false);
                var restamp = Time(_world.World.GetExistingSystem<SightBlockSystem>());
                var stamp = Time(_world.World.GetExistingSystem<FogOfWarSystem>());
                if (i >= 0)
                {
                    total += restamp + stamp;
                    slowest = math.max(slowest, restamp + stamp);
                }
            }

            TestContext.WriteLine($"a section a second: restamp and fog mean {total / Rounds:F3} ms, max {slowest:F3} ms");
            Assert.Less(total / Rounds, 2.6, $"mean {total / Rounds:F3} ms, under twice 0.1 ms of blockers and 1.2 ms of fog");
        }

        [Test]
        public void ForestEdge_TwoHundredViewers()
        {
            var units = Viewers(100, new float2(80f, -150f), new float2(110f, 150f));
            var stamp = Measure(round => Shift(units, 0, units.Length, round));

            TestContext.WriteLine($"200 viewers at a forest edge, all moving: stamp mean {stamp.x:F3} ms, max {stamp.y:F3} ms");
            Assert.Less(stamp.x, 2.4, $"mean {stamp.x:F3} ms, under twice 1.2 ms");
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
                _world.EntityManager.SetComponentData(units[i], new VisionRange { Value = 35f, EyeHeight = 2f });
            }

            return units;
        }

        /// <summary>
        /// Moves units east at 7.5 m/s, a brisk vehicle: 0.75 m a stamp, so about a third of them cross into another
        /// fog cell each stamp, as the plan's budget assumes.
        /// </summary>
        private void Shift(Entity[] units, int from, int count, int round)
        {
            const float step = 0.75f;
            for (var i = from; i < from + count; i++)
            {
                var transform = _world.Get<LocalTransform>(units[i]);
                transform.Position.x += step;
                _world.EntityManager.SetComponentData(units[i], transform);
            }
        }

        /// <summary>Mean and slowest fog stamp in milliseconds after each change.</summary>
        private float2 Measure(System.Action<int> change)
        {
            Run();
            double total = 0.0, slowest = 0.0;
            for (var round = -1; round < Rounds; round++)
            {
                change(round + 1);
                var time = Time(_world.World.GetExistingSystem<FogOfWarSystem>());
                if (round >= 0)
                {
                    total += time;
                    slowest = math.max(slowest, time);
                }
            }

            return new float2((float)(total / Rounds), (float)slowest);
        }

        private void Run()
        {
            Time(_world.World.GetExistingSystem<SightBlockSystem>());
            Time(_world.World.GetExistingSystem<FogOfWarSystem>());
        }

        /// <summary>Updates one system past the fog's interval and waits for its jobs.</summary>
        private double Time(SystemHandle system)
        {
            _time += FogOfWarSystem.UpdateInterval * 2f;
            _world.World.SetTime(new TimeData(_time, FogOfWarSystem.UpdateInterval));
            _world.EntityManager.CompleteAllTrackedJobs();
            var watch = Stopwatch.StartNew();
            system.Update(_world.World.Unmanaged);
            _world.EntityManager.CompleteAllTrackedJobs();
            return watch.Elapsed.TotalMilliseconds;
        }
    }
}
