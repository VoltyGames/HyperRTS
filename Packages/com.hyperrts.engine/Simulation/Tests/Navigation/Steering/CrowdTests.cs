using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Navigation;
using HyperRTS.Simulation.Orders;
using HyperRTS.Simulation.Units;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.Tests.Navigation.Steering
{
    /// <summary>
    /// Movers steering round each other before they touch, parked units giving way or holding, and groups arriving
    /// together or giving up.
    /// </summary>
    public class CrowdTests
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

        // Largest distance the unit is pushed from where it stands over the given time.
        private float Shove(Entity unit, float seconds)
        {
            var start = _world.GetPosition(unit).xz;
            var shoved = 0f;
            for (var t = 0f; t < seconds; t += TestWorld.FrameTime)
            {
                _world.Tick();
                shoved = math.max(shoved, math.distance(start, _world.GetPosition(unit).xz));
            }

            return shoved;
        }

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
            Assert.AreNotEqual(ArrivalReason.Stalled, _world.Get<Arrival>(unit).Reason,
                "reached the goal, not given up");
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
        public void IdleAlly_InAOneUnitGap_StepsAside_AndAColumnGetsThrough()
        {
            // A lane one cell wide, z 0 to 1, from x -3 to 3.
            _world.SpawnObstacle(new float3(0f, 0f, 2f), new float2(6f, 2f));
            _world.SpawnObstacle(new float3(0f, 0f, -1f), new float2(6f, 2f));
            _world.Tick();

            var parked = _world.SpawnUnit(1, new float3(0f, 0f, 0.5f), radius: 0.4f);
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

            var shoved = Shove(parked, 20f);

            Assert.Greater(shoved, 1f, "the idle unit made way");
            for (var i = 0; i < column.Length; i++)
            {
                Assert.IsFalse(_world.IsEnabled<MoveDestination>(column[i]), "the move ended");
                Assert.Less(math.distance(_world.GetPosition(column[i]).xz, goals[i].xz), 1f, "got through the lane");
            }
        }

        [Test]
        public void ConstructingDozer_IsNeverShoved_TheMoverGoesRound()
        {
            var site = _world.SpawnBuilding(1, new float3(0f, 0f, 4f), new float2(4f, 4f), complete: false,
                buildTime: 60f);
            var dozer = _world.MakeBuilder(_world.SpawnUnit(1, float3.zero));
            _world.Order(dozer, OrderType.Build, site);
            Assert.IsTrue(_world.RunUntil(() => !_world.IsEnabled<MoveDestination>(dozer), 5f), "reached the site");
            var at = _world.GetPosition(dozer);

            var truck = _world.SpawnUnit(1, new float3(-8f, 0f, at.z));
            var goal = new float3(8f, 0f, at.z);
            _world.MoveTo(truck, goal);

            Assert.AreEqual(0f, Shove(dozer, 8f), 1e-4f, "the dozer held its ground");
            Assert.AreEqual(OrderType.Build, _world.Get<ActiveOrder>(dozer).Value.Type, "still building");
            Assert.Less(math.distance(_world.GetPosition(truck).xz, goal.xz), 1f, "the truck went round it");
        }

        [Test]
        public void MovingUnit_DrivesRoundAParkedEnemyHeadOn_WithoutShovingIt()
        {
            var parked = _world.SpawnUnit(2, float3.zero);
            var unit = _world.SpawnUnit(1, new float3(-8f, 0f, 0f));
            var goal = new float3(8f, 0f, 0f);
            _world.MoveTo(unit, goal);

            Assert.AreEqual(0f, Shove(parked, 6f), 1e-4f, "the enemy held its ground");
            Assert.Less(math.distance(_world.GetPosition(unit).xz, goal.xz), 0.2f, "went round it");
        }

        [Test]
        public void MovingUnit_WaitsAgainstAnEnemyOnItsGoal_ThenGivesUp()
        {
            var goal = new float3(8f, 0f, 0f);
            var parked = _world.SpawnUnit(2, goal);
            var unit = _world.SpawnUnit(1, float3.zero);
            _world.MoveTo(unit, goal);
            _world.Run(2f + MoveProgress.GiveUpSeconds);

            Assert.IsFalse(_world.IsEnabled<ActiveOrder>(unit), "the blocked move completed");
            Assert.IsTrue(_world.IsEnabled<Arrival>(unit) &&
                _world.Get<Arrival>(unit).Reason == ArrivalReason.Stalled, "it gave up");
            // Once both are parked they share the last frame's press, a few centimetres.
            Assert.AreEqual(goal.x, _world.GetPosition(parked).x, 0.1f, "never shoved off the goal");
            Assert.Less(math.distance(_world.GetPosition(unit).xz, goal.xz), 1.5f, "waited beside it, not circling");
        }

        [Test]
        public void Column_QueuedAtAChoke_FarFromItsGoals_KeepsItsOrders()
        {
            // A lane one cell wide, z 0 to 1, from x -15 to 15, too long to go round, shut by an enemy standing in it.
            _world.SpawnObstacle(new float3(0f, 0f, 2f), new float2(30f, 2f));
            _world.SpawnObstacle(new float3(0f, 0f, -1f), new float2(30f, 2f));
            _world.Tick();
            var enemy = _world.SpawnUnit(2, new float3(0f, 0f, 0.5f), radius: 0.4f);
            var column = new Entity[3];
            for (var i = 0; i < column.Length; i++)
            {
                column[i] = _world.SpawnUnit(1, new float3(-18f - i * 1.5f, 0f, 0.5f), radius: 0.4f);
                var goal = new float3(25f + i * 2f, 0f, 0.5f);
                _world.Issue(column[i], new Order { Type = OrderType.Move, Position = goal });
            }

            _world.Run(MoveProgress.GiveUpSeconds * 2f);
            foreach (var unit in column)
            {
                Assert.IsTrue(_world.IsEnabled<ActiveOrder>(unit), "still queued, not given up");
            }

            _world.EntityManager.SetComponentData(enemy, LocalTransform.FromPosition(new float3(0f, 0f, 30f)));
            var done = _world.RunUntil(() => System.Array.TrueForAll(column, u => !_world.IsEnabled<ActiveOrder>(u)),
                30f);

            Assert.IsTrue(done, "the column went through once the lane cleared");
            foreach (var unit in column)
            {
                Assert.Greater(_world.GetPosition(unit).x, 20f, "reached its goal");
            }
        }

        [Test]
        public void Group_SharingAGoal_ArrivesByTouch_WithoutStalling()
        {
            var goal = new float3(8f, 0f, 0f);
            var units = new[]
            {
                _world.SpawnUnit(1, new float3(0f, 0f, 0f)),
                _world.SpawnUnit(1, new float3(-1.5f, 0f, 0f)),
                _world.SpawnUnit(1, new float3(-3f, 0f, 0f)),
            };
            foreach (var unit in units)
            {
                _world.Issue(unit, new Order { Type = OrderType.Move, Position = goal, Group = 7, Extent = 4f });
            }

            var done = _world.RunUntil(() => System.Array.TrueForAll(units, u => !_world.IsEnabled<ActiveOrder>(u)),
                4f);

            Assert.IsTrue(done, "every move finished sooner than a stall");
            foreach (var unit in units)
            {
                Assert.IsTrue(_world.IsEnabled<Arrival>(unit), "arrived");
                Assert.AreNotEqual(ArrivalReason.Stalled, _world.Get<Arrival>(unit).Reason,
                    "by the goal or a groupmate, not by giving up");
            }
        }

        [Test]
        public void Groupmate_ParkedOnTheRoute_FarFromTheGoalArea_DoesNotEndTheMove()
        {
            var parked = _world.SpawnUnit(1, new float3(0f, 0f, 0f));
            var mover = _world.SpawnUnit(1, new float3(-10f, 0f, 0f));
            var goal = new float3(25f, 0f, 0f);
            _world.Issue(parked, new Order { Type = OrderType.Move, Position = new float3(0.5f, 0f, 0f), Group = 7,
                Extent = 4f });
            _world.Issue(mover, new Order { Type = OrderType.Move, Position = goal, Group = 7, Extent = 4f });

            Assert.IsTrue(_world.RunUntil(() => !_world.IsEnabled<ActiveOrder>(mover), 15f), "the move ended");

            Assert.Less(math.distance(_world.GetPosition(mover).xz, goal.xz), 1f, "went on past it to its own slot");
        }

        [Test]
        public void Groupmate_StoppedShortAgainstAnother_DoesNotEndTheMove()
        {
            var parked = _world.SpawnUnit(1, float3.zero);
            var goal = new float3(8f, 0f, 0f);
            _world.EntityManager.SetComponentData(parked, new MoveDestination { Value = goal, Group = 7 });
            _world.EntityManager.SetComponentData(parked, new Arrival { Reason = ArrivalReason.AtGroupmate });
            _world.EntityManager.SetComponentEnabled<Arrival>(parked, true);
            var mover = _world.SpawnUnit(1, new float3(-6f, 0f, 0f));
            _world.Issue(mover, new Order { Type = OrderType.Move, Position = goal, Group = 7, Extent = 30f });

            Assert.IsTrue(_world.RunUntil(() => !_world.IsEnabled<ActiveOrder>(mover), 15f), "the move ended");

            var left = math.distance(_world.GetPosition(mover).xz, goal.xz);
            Assert.Less(left, 1f, "a groupmate that stopped short doesn't stop the next one short");
        }
    }
}
