using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Navigation;
using HyperRTS.Simulation.Units;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Tests
{
    /// <summary>Movers steering round each other before they touch, and round idle units that give way.</summary>
    public class AvoidanceTests
    {
        private TestWorld _world;

        [SetUp]
        public void SetUp()
        {
            _world = new TestWorld();
            _world.CreateMatch(1, 2);
        }

        [TearDown]
        public void TearDown() => _world.Dispose();

        private bool AllArrived(Entity[] units) =>
            System.Array.TrueForAll(units, unit => !_world.IsEnabled<MoveDestination>(unit));

        // Closest the first two units come and the slowest any unit drives between the start and its last frame.
        private (float closest, float slowest) Watch(Entity[] units, float seconds)
        {
            var closest = float.MaxValue;
            var slowest = float.MaxValue;
            for (var t = 0f; t < seconds && !AllArrived(units); t += TestWorld.FrameTime)
            {
                var before = System.Array.ConvertAll(units, unit => _world.GetPosition(unit).xz);
                _world.Tick();
                for (var i = 0; i < units.Length; i++)
                {
                    // The last frame only covers what is left, so it isn't a stop.
                    if (_world.IsEnabled<MoveDestination>(units[i]))
                    {
                        var speed = math.distance(before[i], _world.GetPosition(units[i]).xz) / TestWorld.FrameTime;
                        slowest = math.min(slowest, speed);
                    }
                }

                closest = math.min(closest, math.distance(_world.GetPosition(units[0]).xz,
                    _world.GetPosition(units[1]).xz));
            }

            return (closest, slowest);
        }

        private void AssertArrived(Entity unit, float3 goal, float tolerance)
        {
            Assert.IsTrue(_world.IsEnabled<Arrival>(unit), "arrived");
            Assert.IsFalse(_world.Get<Arrival>(unit).Stalled, "reached the goal, not given up");
            Assert.Less(math.distance(_world.GetPosition(unit).xz, goal.xz), tolerance, "at its goal");
        }

        [Test]
        public void HeadOnPair_PassesEachOther_WithoutStallingOrTouching()
        {
            var a = _world.SpawnUnit(1, new float3(-10f, 0f, 0f));
            var b = _world.SpawnUnit(2, new float3(10f, 0f, 0f));
            var goalA = new float3(10f, 0f, 0f);
            var goalB = new float3(-10f, 0f, 0f);
            _world.MoveTo(a, goalA);
            _world.MoveTo(b, goalB);
            _world.Tick();

            var (closest, _) = Watch(new[] { a, b }, 8f);

            Assert.Greater(closest, 0.98f, "never touched");
            AssertArrived(a, goalA, 0.3f);
            AssertArrived(b, goalB, 0.3f);
        }

        [Test]
        public void CrossingPair_AtRightAngles_AvoidsWithoutStopping()
        {
            var a = _world.SpawnUnit(1, new float3(-10f, 0f, 0f));
            var b = _world.SpawnUnit(1, new float3(0f, 0f, -10f));
            var goalA = new float3(10f, 0f, 0f);
            var goalB = new float3(0f, 0f, 10f);
            _world.MoveTo(a, goalA);
            _world.MoveTo(b, goalB);
            _world.Tick();

            var (closest, slowest) = Watch(new[] { a, b }, 8f);

            Assert.Greater(closest, 0.98f, "never touched");
            Assert.Greater(slowest, 1f, "neither stopped to let the other by");
            AssertArrived(a, goalA, 0.3f);
            AssertArrived(b, goalB, 0.3f);
        }

        [Test]
        public void Mover_ThreadsACrowdOfMovingUnits_ToItsGoal()
        {
            for (var i = 0; i < 5; i++)
            {
                var up = _world.SpawnUnit(2, new float3(-6f + i * 3f, 0f, -8f));
                _world.MoveTo(up, new float3(-6f + i * 3f, 0f, 8f));
                var down = _world.SpawnUnit(2, new float3(-4.5f + i * 3f, 0f, 8f));
                _world.MoveTo(down, new float3(-4.5f + i * 3f, 0f, -8f));
            }

            var unit = _world.SpawnUnit(1, new float3(-15f, 0f, 0f));
            var goal = new float3(15f, 0f, 0f);
            _world.MoveTo(unit, goal);
            _world.Tick();

            Assert.IsTrue(_world.RunUntil(() => !_world.IsEnabled<MoveDestination>(unit), 15f), "the move ended");
            AssertArrived(unit, goal, 0.3f);
        }

        [Test]
        public void Column_ThroughAnIdleAlly_InAOneUnitGap_GetsThrough()
        {
            // A lane one cell wide, z 0 to 1, from x -3 to 3.
            _world.SpawnObstacle(new float3(0f, 0f, 2f), new float2(6f, 2f));
            _world.SpawnObstacle(new float3(0f, 0f, -1f), new float2(6f, 2f));
            _world.Tick();

            _world.SpawnUnit(1, new float3(0f, 0f, 0.5f), radius: 0.4f);
            var column = new[]
            {
                _world.SpawnUnit(1, new float3(-8f, 0f, 0.5f), radius: 0.4f),
                _world.SpawnUnit(1, new float3(-10f, 0f, 0.5f), radius: 0.4f),
            };
            var goals = new[] { new float3(9f, 0f, 0.5f), new float3(7f, 0f, 0.5f) };
            for (var i = 0; i < column.Length; i++)
            {
                _world.MoveTo(column[i], goals[i]);
            }

            _world.Tick();

            Assert.IsTrue(_world.RunUntil(() => AllArrived(column), 20f), "both moves ended");
            for (var i = 0; i < column.Length; i++)
            {
                Assert.Less(math.distance(_world.GetPosition(column[i]).xz, goals[i].xz), 1f, "got through the lane");
            }
        }
    }
}
