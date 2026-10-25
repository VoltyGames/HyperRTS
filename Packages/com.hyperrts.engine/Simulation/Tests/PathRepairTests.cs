using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Navigation;
using HyperRTS.Simulation.Units;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.Tests
{
    /// <summary>A unit shoved off its path re-plans from where it stands and still arrives.</summary>
    public class PathRepairTests
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

        private float3 FirstCorner(Entity unit) => _world.EntityManager.GetBuffer<PathWaypoint>(unit)[0].Position;

        [Test]
        public void ShovedBehindAPillar_StillArrives()
        {
            // A wall from z -6 to 14 whose south end is the way round, and a pillar north of the start.
            SpawnObstacle(new float3(0.5f, 0f, 4f), new float2(1f, 20f));
            SpawnObstacle(new float3(-2f, 0f, 10.5f), new float2(2f, 1f));
            _world.Tick();

            var unit = _world.SpawnUnit(1, new float3(-5f, 0f, 0f));
            var goal = new float3(5f, 0f, 0f);
            _world.MoveTo(unit, goal);
            _world.Run(0.3f);
            var corner = FirstCorner(unit);
            Assert.Less(corner.z, -6f, "the path rounds the wall's south end");

            // From here the north end is far shorter, so a fresh search would turn the unit round.
            var transform = _world.Get<LocalTransform>(unit);
            transform.Position = new float3(-2f, 0f, 12f);
            _world.EntityManager.SetComponentData(unit, transform);

            var arrived = _world.RunUntil(() => !_world.IsEnabled<MoveDestination>(unit), 20f);
            Assert.IsTrue(arrived, "the move ended");
            Assert.IsFalse(_world.Get<Arrival>(unit).Stalled, "by arriving, not by giving up");
            Assert.Less(math.distance(_world.GetPosition(unit).xz, goal.xz), 1f, "at the goal");
        }
    }
}
