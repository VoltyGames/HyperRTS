using HyperRTS.Simulation.Navigation;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Tests.Navigation
{
    public partial class NavBenchmarkTests
    {
        [Test]
        public void SingleMoves_CrossMap_KeepToTheFrameBudget()
        {
            var lone = SendAcross(1, 0f, 60f);
            var single = Searched(lone);

            var units = SendAcross(120, 3f, -20f);
            var before = Stats();
            var overshoot = 0f;
            var frames = 0;
            for (; frames < 120 && !AllReady(units); frames++)
            {
                var start = Stats();
                _world.Tick();
                var spent = Stats().Estimated - start.Estimated;
                overshoot = math.max(overshoot, spent - PathfindingSystem.CostPerFrame);
            }

            var after = Stats();
            var estimated = after.Estimated - before.Estimated;
            var expanded = after.Expansions - before.Expansions;
            TestContext.WriteLine($"{after.Searches - before.Searches} searches over {frames} frames, one about " +
                                  $"{single:F0}; estimated {estimated}, expanded {expanded}, overshoot {overshoot:F0}");
            Assert.IsTrue(AllReady(units), "every unit has a path");
            Assert.Less(overshoot, single * 1.5f, "over the budget by one search at most");
            Assert.That((double)expanded / estimated, Is.InRange(0.25, 4.0), "the estimate tracks the expansions");
        }

        // Units on the west edge each sent alone, no group, to the east edge.
        private Entity[] SendAcross(int count, float spacing, float z)
        {
            var units = new Entity[count];
            for (var i = 0; i < count; i++)
            {
                var row = new float3(i % 12 * spacing, 0f, i / 12 * spacing);
                units[i] = _world.SpawnUnit(1, new float3(-285f, 0f, z) + row);
            }

            _world.Tick();
            for (var i = 0; i < count; i++)
            {
                var goal = new float3(245f, 0f, z) + new float3(i % 12 * spacing, 0f, i / 12 * spacing);
                _world.EntityManager.SetComponentData(units[i], new MoveDestination { Value = goal });
                _world.EntityManager.SetComponentEnabled<MoveDestination>(units[i], true);
            }

            return units;
        }

        // The estimate of the one search the units ask for.
        private float Searched(Entity[] units)
        {
            var before = Stats();
            for (var frame = 0; frame < 10 && !AllReady(units); frame++)
            {
                _world.Tick();
            }

            Assert.AreEqual(1, Stats().Searches - before.Searches, "one search");
            return Stats().Estimated - before.Estimated;
        }
    }
}
