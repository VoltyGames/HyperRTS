using HyperRTS.Simulation.Navigation.Paths;
using NUnit.Framework;
using Unity.PerformanceTesting;
using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Tests.Navigation
{
    /// <summary>
    /// Navigation at scale on a 600 m map at 1 m cells with about 80 rotated buildings, woods and a river with two
    /// bridges. Timings are recorded for the performance results, never asserted: they vary with the machine and Editor.
    /// </summary>
    [Category("Benchmark")]
    public class NavBenchmarkTests : NavBenchmarkFixture
    {
        [Test, Performance]
        public void GroupOrder_CrossMap_GivesEveryMemberAPath()
        {
            const int count = 200;
            var group = 0;
            Entity[] units = null;
            var before = Stats();
            var median = BenchmarkKit.Median($"GroupOrder{count}", () => Serve(units),
                () => units = Order(count, ++group), () =>
                {
                    foreach (var unit in units)
                    {
                        Assert.AreEqual(PathStatus.Ready, _world.Get<PathState>(unit).Status, "every member has a path");
                        _world.EntityManager.DestroyEntity(unit);
                    }
                }, "FlowField.Build", "FlowField.Trace", "GridAStar.Search");

            var after = Stats();
            TestContext.WriteLine($"{count}-unit group order: median {median:F2} ms, " +
                                  $"{after.Fields - before.Fields} fields, {after.Traces - before.Traces} traces, " +
                                  $"{after.Searches - before.Searches} searches");
        }

        // A block of units on the west edge sent to a block of slots on the east edge, a new place each group.
        private Entity[] Order(int count, int group)
        {
            var side = (int)math.ceil(math.sqrt(count));
            var place = group % 6;
            var units = new Entity[count];
            for (var i = 0; i < count; i++)
            {
                var start = new float3(-285f + i % side * 3f, 0f, -20f + i / side * 3f + place * 60f - 120f);
                units[i] = _world.SpawnUnit(1, start);
            }

            _world.Tick();
            for (var i = 0; i < count; i++)
            {
                var slot = new float3(245f + i % side * 3f, 0f, -20f + i / side * 3f + place * 50f - 100f);
                _world.Send(units[i], slot, group);
            }

            return units;
        }

        // Only the pathfinder runs, until every unit has a path.
        private void Serve(Entity[] units)
        {
            for (var frame = 0; frame < 20 && !AllReady(units); frame++)
            {
                _world.UpdateSystem<PathfindingSystem>();
            }
        }

    }
}
