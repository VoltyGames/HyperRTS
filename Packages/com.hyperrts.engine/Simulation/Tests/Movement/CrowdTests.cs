using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Navigation.Agents;
using HyperRTS.Simulation.Orders;
using HyperRTS.Simulation.Movement;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.Tests.Movement
{
    /// <summary>
    /// Movers steering round each other before they touch, parked units giving way or holding, and groups arriving
    /// together or giving up.
    /// </summary>
    public class CrowdTests : SimulationFixture
    {
        // Largest distance the unit is pushed from where it stands over the given time.
        private float Shove(Entity unit, float seconds)
        {
            var start = World.GetPosition(unit).xz;
            var shoved = 0f;
            for (var t = 0f; t < seconds; t += TestWorld.FrameTime)
            {
                World.Tick();
                shoved = math.max(shoved, math.distance(start, World.GetPosition(unit).xz));
            }

            return shoved;
        }

        private bool AllArrived(Entity[] units) =>
            System.Array.TrueForAll(units, unit => !World.IsEnabled<MoveDestination>(unit));

        // Closest the first two units come and the slowest any unit drives between the start and its last frame.
        private (float closest, float slowest) Watch(Entity[] units, float seconds)
        {
            var closest = float.MaxValue;
            var slowest = float.MaxValue;
            for (var t = 0f; t < seconds && !AllArrived(units); t += TestWorld.FrameTime)
            {
                var before = System.Array.ConvertAll(units, unit => World.GetPosition(unit).xz);
                World.Tick();
                for (var i = 0; i < units.Length; i++)
                {
                    // The last frame only covers what is left, so it isn't a stop.
                    if (World.IsEnabled<MoveDestination>(units[i]))
                    {
                        var speed = math.distance(before[i], World.GetPosition(units[i]).xz) / TestWorld.FrameTime;
                        slowest = math.min(slowest, speed);
                    }
                }

                closest = math.min(closest, math.distance(World.GetPosition(units[0]).xz,
                    World.GetPosition(units[1]).xz));
            }

            return (closest, slowest);
        }

        private void AssertArrived(Entity unit, float3 goal, float tolerance)
        {
            Assert.IsTrue(World.IsEnabled<Arrival>(unit), "arrived");
            Assert.AreNotEqual(ArrivalReason.Stalled, World.Get<Arrival>(unit).Reason,
                "reached the goal, not given up");
            Assert.Less(math.distance(World.GetPosition(unit).xz, goal.xz), tolerance, "at its goal");
        }

        [Test]
        public void HeadOnPair_PassesEachOther_WithoutStallingOrTouching()
        {
            var a = World.SpawnUnit(1, new float3(-10f, 0f, 0f));
            var b = World.SpawnUnit(2, new float3(10f, 0f, 0f));
            var goalA = new float3(10f, 0f, 0f);
            var goalB = new float3(-10f, 0f, 0f);
            World.MoveTo(a, goalA);
            World.MoveTo(b, goalB);
            World.Tick();

            var (closest, _) = Watch(new[] { a, b }, 8f);

            Assert.Greater(closest, 0.98f, "never touched");
            AssertArrived(a, goalA, 0.3f);
            AssertArrived(b, goalB, 0.3f);
        }

        [Test]
        public void CrossingPair_AtRightAngles_AvoidsWithoutStopping()
        {
            var a = World.SpawnUnit(1, new float3(-10f, 0f, 0f));
            var b = World.SpawnUnit(1, new float3(0f, 0f, -10f));
            var goalA = new float3(10f, 0f, 0f);
            var goalB = new float3(0f, 0f, 10f);
            World.MoveTo(a, goalA);
            World.MoveTo(b, goalB);
            World.Tick();

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
                var up = World.SpawnUnit(2, new float3(-6f + i * 3f, 0f, -8f));
                World.MoveTo(up, new float3(-6f + i * 3f, 0f, 8f));
                var down = World.SpawnUnit(2, new float3(-4.5f + i * 3f, 0f, 8f));
                World.MoveTo(down, new float3(-4.5f + i * 3f, 0f, -8f));
            }

            var unit = World.SpawnUnit(1, new float3(-15f, 0f, 0f));
            var goal = new float3(15f, 0f, 0f);
            World.MoveTo(unit, goal);
            World.Tick();

            Assert.IsTrue(World.RunUntil(() => !World.IsEnabled<MoveDestination>(unit), 15f), "the move ended");
            AssertArrived(unit, goal, 0.3f);
        }

        [Test]
        public void IdleAlly_InAOneUnitGap_StepsAside_AndAColumnGetsThrough()
        {
            // A lane one cell wide, z 0 to 1, from x -3 to 3.
            World.SpawnObstacle(new float3(0f, 0f, 2f), new float2(6f, 2f));
            World.SpawnObstacle(new float3(0f, 0f, -1f), new float2(6f, 2f));
            World.Tick();

            var parked = World.SpawnUnit(1, new float3(0f, 0f, 0.5f), radius: 0.4f);
            var column = new[]
            {
                World.SpawnUnit(1, new float3(-8f, 0f, 0.5f), radius: 0.4f),
                World.SpawnUnit(1, new float3(-10f, 0f, 0.5f), radius: 0.4f),
            };
            var goals = new[] { new float3(9f, 0f, 0.5f), new float3(7f, 0f, 0.5f) };
            for (var i = 0; i < column.Length; i++)
            {
                World.MoveTo(column[i], goals[i]);
            }

            var shoved = Shove(parked, 20f);

            Assert.Greater(shoved, 1f, "the idle unit made way");
            for (var i = 0; i < column.Length; i++)
            {
                Assert.IsFalse(World.IsEnabled<MoveDestination>(column[i]), "the move ended");
                Assert.Less(math.distance(World.GetPosition(column[i]).xz, goals[i].xz), 1f, "got through the lane");
            }
        }

        [Test]
        public void ConstructingDozer_IsNeverShoved_TheMoverGoesRound()
        {
            var site = World.SpawnBuilding(1, new float3(0f, 0f, 4f), new float2(4f, 4f), complete: false,
                buildTime: 60f);
            var dozer = World.MakeBuilder(World.SpawnUnit(1, float3.zero));
            World.Order(dozer, OrderType.Build, site);
            Assert.IsTrue(World.RunUntil(() => !World.IsEnabled<MoveDestination>(dozer), 5f), "reached the site");
            var at = World.GetPosition(dozer);

            var truck = World.SpawnUnit(1, new float3(-8f, 0f, at.z));
            var goal = new float3(8f, 0f, at.z);
            World.MoveTo(truck, goal);

            Assert.AreEqual(0f, Shove(dozer, 8f), 1e-4f, "the dozer held its ground");
            Assert.AreEqual(OrderType.Build, World.Get<ActiveOrder>(dozer).Value.Type, "still building");
            Assert.Less(math.distance(World.GetPosition(truck).xz, goal.xz), 1f, "the truck went round it");
        }

        [Test]
        public void MovingUnit_DrivesRoundAParkedEnemyHeadOn_WithoutShovingIt()
        {
            var parked = World.SpawnUnit(2, float3.zero);
            var unit = World.SpawnUnit(1, new float3(-8f, 0f, 0f));
            var goal = new float3(8f, 0f, 0f);
            World.MoveTo(unit, goal);

            Assert.AreEqual(0f, Shove(parked, 6f), 1e-4f, "the enemy held its ground");
            Assert.Less(math.distance(World.GetPosition(unit).xz, goal.xz), 0.2f, "went round it");
        }

        [Test]
        public void MovingUnit_WaitsAgainstAnEnemyOnItsGoal_ThenGivesUp()
        {
            var goal = new float3(8f, 0f, 0f);
            var parked = World.SpawnUnit(2, goal);
            var unit = World.SpawnUnit(1, float3.zero);
            World.MoveTo(unit, goal);
            World.Run(2f + MoveProgress.GiveUpSeconds);

            Assert.IsFalse(World.IsEnabled<ActiveOrder>(unit), "the blocked move completed");
            Assert.IsTrue(World.IsEnabled<Arrival>(unit) &&
                World.Get<Arrival>(unit).Reason == ArrivalReason.Stalled, "it gave up");
            // Once both are parked they share the last frame's press, a few centimetres.
            Assert.AreEqual(goal.x, World.GetPosition(parked).x, 0.1f, "never shoved off the goal");
            Assert.Less(math.distance(World.GetPosition(unit).xz, goal.xz), 1.5f, "waited beside it, not circling");
        }

        [Test]
        public void Column_QueuedAtAChoke_FarFromItsGoals_KeepsItsOrders()
        {
            // A lane one cell wide, z 0 to 1, from x -15 to 15, too long to go round, shut by an enemy standing in it.
            World.SpawnObstacle(new float3(0f, 0f, 2f), new float2(30f, 2f));
            World.SpawnObstacle(new float3(0f, 0f, -1f), new float2(30f, 2f));
            World.Tick();
            var enemy = World.SpawnUnit(2, new float3(0f, 0f, 0.5f), radius: 0.4f);
            var column = new Entity[3];
            for (var i = 0; i < column.Length; i++)
            {
                column[i] = World.SpawnUnit(1, new float3(-18f - i * 1.5f, 0f, 0.5f), radius: 0.4f);
                var goal = new float3(25f + i * 2f, 0f, 0.5f);
                World.Issue(column[i], new Order { Type = OrderType.Move, Position = goal });
            }

            World.Run(MoveProgress.GiveUpSeconds * 2f);
            foreach (var unit in column)
            {
                Assert.IsTrue(World.IsEnabled<ActiveOrder>(unit), "still queued, not given up");
            }

            World.Set(enemy, LocalTransform.FromPosition(new float3(0f, 0f, 30f)));
            var done = World.RunUntil(() => System.Array.TrueForAll(column, u => !World.IsEnabled<ActiveOrder>(u)),
                30f);

            Assert.IsTrue(done, "the column went through once the lane cleared");
            foreach (var unit in column)
            {
                Assert.Greater(World.GetPosition(unit).x, 20f, "reached its goal");
            }
        }

        [Test]
        public void Group_SharingAGoal_ArrivesByTouch_WithoutStalling()
        {
            var goal = new float3(8f, 0f, 0f);
            var units = new[]
            {
                World.SpawnUnit(1, new float3(0f, 0f, 0f)),
                World.SpawnUnit(1, new float3(-1.5f, 0f, 0f)),
                World.SpawnUnit(1, new float3(-3f, 0f, 0f)),
            };
            foreach (var unit in units)
            {
                World.Issue(unit, new Order { Type = OrderType.Move, Position = goal, Group = 7, Extent = 4f });
            }

            var done = World.RunUntil(() => System.Array.TrueForAll(units, u => !World.IsEnabled<ActiveOrder>(u)),
                4f);

            Assert.IsTrue(done, "every move finished sooner than a stall");
            foreach (var unit in units)
            {
                Assert.IsTrue(World.IsEnabled<Arrival>(unit), "arrived");
                Assert.AreNotEqual(ArrivalReason.Stalled, World.Get<Arrival>(unit).Reason,
                    "by the goal or a groupmate, not by giving up");
            }
        }

        [Test]
        public void Groupmate_ParkedOnTheRoute_FarFromTheGoalArea_DoesNotEndTheMove()
        {
            var parked = World.SpawnUnit(1, new float3(0f, 0f, 0f));
            var mover = World.SpawnUnit(1, new float3(-10f, 0f, 0f));
            var goal = new float3(25f, 0f, 0f);
            World.Issue(parked, new Order { Type = OrderType.Move, Position = new float3(0.5f, 0f, 0f), Group = 7,
                Extent = 4f });
            World.Issue(mover, new Order { Type = OrderType.Move, Position = goal, Group = 7, Extent = 4f });

            Assert.IsTrue(World.RunUntil(() => !World.IsEnabled<ActiveOrder>(mover), 15f), "the move ended");

            Assert.Less(math.distance(World.GetPosition(mover).xz, goal.xz), 1f, "went on past it to its own slot");
        }

        [Test]
        public void Groupmate_StoppedShortAgainstAnother_DoesNotEndTheMove()
        {
            var parked = World.SpawnUnit(1, float3.zero);
            var goal = new float3(8f, 0f, 0f);
            World.Set(parked, new MoveDestination { Value = goal, Group = 7 });
            World.Set(parked, new Arrival { Reason = ArrivalReason.AtGroupmate });
            World.SetEnabled<Arrival>(parked, true);
            var mover = World.SpawnUnit(1, new float3(-6f, 0f, 0f));
            World.Issue(mover, new Order { Type = OrderType.Move, Position = goal, Group = 7, Extent = 30f });

            Assert.IsTrue(World.RunUntil(() => !World.IsEnabled<ActiveOrder>(mover), 15f), "the move ended");

            var left = math.distance(World.GetPosition(mover).xz, goal.xz);
            Assert.Less(left, 1f, "a groupmate that stopped short doesn't stop the next one short");
        }
    }
}
