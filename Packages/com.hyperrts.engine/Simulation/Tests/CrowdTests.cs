using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Navigation;
using HyperRTS.Simulation.Orders;
using HyperRTS.Simulation.Units;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.Tests
{
    /// <summary>Parked units giving way or holding against movers, groups arriving together and giving up.</summary>
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

        private void SpawnObstacle(float3 center, float2 size)
        {
            var obstacle = _world.EntityManager.CreateEntity();
            _world.EntityManager.AddComponentData(obstacle, LocalTransform.FromPosition(center));
            var writer = new EntityManagerWriter(_world.EntityManager, obstacle);
            NavSetup.AddObstacle(ref writer, size);
        }

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

        [Test]
        public void IdleAlly_InAOneUnitGap_StepsAside_AndTheMoverGetsThrough()
        {
            // A lane one cell wide, z 0 to 1, from x -3 to 3.
            SpawnObstacle(new float3(0f, 0f, 2f), new float2(6f, 2f));
            SpawnObstacle(new float3(0f, 0f, -1f), new float2(6f, 2f));
            _world.Tick();

            var parked = _world.SpawnUnit(1, new float3(0f, 0f, 0.5f), radius: 0.4f);
            var unit = _world.SpawnUnit(1, new float3(-8f, 0f, 0.5f), radius: 0.4f);
            var goal = new float3(8f, 0f, 0.5f);
            _world.MoveTo(unit, goal);

            var shoved = Shove(parked, 15f);

            Assert.Less(math.distance(_world.GetPosition(unit).xz, goal.xz), 1f, "the mover got through the lane");
            Assert.Greater(shoved, 1f, "the idle unit made way");
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
            Assert.IsTrue(_world.IsEnabled<Arrival>(unit) && _world.Get<Arrival>(unit).Stalled, "it gave up");
            // Once both are parked they share the last frame's press, a few centimetres.
            Assert.AreEqual(goal.x, _world.GetPosition(parked).x, 0.1f, "never shoved off the goal");
            Assert.Less(math.distance(_world.GetPosition(unit).xz, goal.xz), 1.5f, "waited beside it, not circling");
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
                _world.Issue(unit, new Order { Type = OrderType.Move, Position = goal, Group = 7 });
            }

            var done = _world.RunUntil(() => System.Array.TrueForAll(units, u => !_world.IsEnabled<ActiveOrder>(u)),
                4f);

            Assert.IsTrue(done, "every move finished sooner than a stall");
            foreach (var unit in units)
            {
                Assert.IsTrue(_world.IsEnabled<Arrival>(unit), "arrived");
                Assert.IsFalse(_world.Get<Arrival>(unit).Stalled, "by the goal or a groupmate, not by giving up");
            }
        }
    }
}
