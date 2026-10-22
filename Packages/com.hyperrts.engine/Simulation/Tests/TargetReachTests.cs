using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Navigation;
using HyperRTS.Simulation.Orders;
using HyperRTS.Simulation.Resources;
using HyperRTS.Simulation.Units;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.Tests
{
    /// <summary>Walking up to a footprint: reach measured hull edge to footprint edge, goals on the nearest side.</summary>
    public class TargetReachTests
    {
        private const float TruckRadius = 1.25f;
        private const float TruckLength = 10f;
        private const float HalfSegment = TruckLength * 0.5f - TruckRadius;

        private static readonly NavBox Yard = new(float3.zero, quaternion.identity, new float2(16f, 16f));

        private TestWorld _world;

        [SetUp]
        public void SetUp()
        {
            _world = new TestWorld();
            _world.CreateMatch(1, 2);
        }

        [TearDown]
        public void TearDown() => _world.Dispose();

        [Test]
        public void LongHull_NoseAtTheEdge_IsInReach_ThoughItsCentreIsFar()
        {
            var centre = new float2(8f + TruckRadius + 0.5f + HalfSegment, 0f);
            var axis = new float2(HalfSegment, 0f);

            Assert.IsTrue(ReachMath.InReach(Yard, centre, axis, TruckRadius, 2f), "nose at the edge");
            Assert.IsFalse(ReachMath.InReach(Yard, centre + new float2(6f, 0f), axis, TruckRadius, 2f), "well back");
        }

        [Test]
        public void EdgePoint_LiesJustOutsideTheNearestSide()
        {
            Assert.AreEqual(new float2(10f, 3f), Yard.EdgePoint(new float2(30f, 3f), 2f), "beside a side");
            Assert.AreEqual(new float2(1f, -10f), Yard.EdgePoint(new float2(1f, -6f), 2f), "from inside, the nearest side");
            var corner = Yard.EdgePoint(new float2(20f, 20f), 1f);
            Assert.AreEqual(1f, math.distance(corner, new float2(8f, 8f)), 1e-4f, "round a corner");
        }

        [Test]
        public void TurnedFootprint_MeasuresInItsOwnFrame()
        {
            var turned = new NavBox(float3.zero, quaternion.RotateY(math.radians(45f)), new float2(4f, 4f));
            var corner = 2f * math.SQRT2;

            Assert.AreEqual(1f, turned.Gap(new float2(corner + 1f, 0f), new float2(corner + 1f, 0f)), 1e-4f);
        }

        [Test]
        public void LongTruck_DeliversAtTheDropOffEdge_DrivingForward_AndNeverEntersIt()
        {
            var supplies = _world.CreateSupplies();
            var dropOff = _world.SpawnBuilding(1, new float3(-30f, 0f, 0f), new float2(16f, 16f));
            _world.EntityManager.AddComponent<ResourceDropOff>(dropOff);
            var node = _world.SpawnNode(new float3(10f, 0f, 0f), supplies, 100);
            var truck = _world.MakeHarvester(SpawnTruck(new float3(-8f, 0f, 0f)), 10, 10f);
            var footprint = new NavBox(new float3(-30f, 0f, 0f), quaternion.identity, new float2(16f, 16f));
            _world.Order(truck, OrderType.Gather, node);

            var closest = float.MaxValue;
            var slowest = 0f;
            for (var t = 0f; t < 60f && _world.GetResources(1, supplies) < 20; t += TestWorld.FrameTime)
            {
                _world.Tick();
                var transform = _world.Get<LocalTransform>(truck);
                var axis = _world.Get<NavAgent>(truck).SegmentAxis(transform.Rotation);
                closest = math.min(closest, footprint.Gap(transform.Position.xz - axis, transform.Position.xz + axis));
                slowest = math.min(slowest, _world.Get<LocomotionState>(truck).Speed);
            }

            Assert.GreaterOrEqual(_world.GetResources(1, supplies), 20, "two loads delivered");
            Assert.GreaterOrEqual(closest, TruckRadius - 0.05f, "the hull never entered the footprint");
            Assert.Greater(slowest, -0.5f, "the round trips drive forward");
        }

        [Test]
        public void Builder_WalledOffFromItsSite_GivesUpTheOrder()
        {
            // A ring of walls round the site, 4 m clear of it, with no way in.
            var site = _world.SpawnBuilding(1, new float3(20f, 0f, 0f), new float2(4f, 4f), complete: false,
                buildTime: 60f);
            SpawnWall(new float3(20f, 0f, 7f), new float2(16f, 2f));
            SpawnWall(new float3(20f, 0f, -7f), new float2(16f, 2f));
            SpawnWall(new float3(13f, 0f, 0f), new float2(2f, 12f));
            SpawnWall(new float3(27f, 0f, 0f), new float2(2f, 12f));
            var dozer = _world.MakeBuilder(_world.SpawnUnit(1, float3.zero));
            _world.Order(dozer, OrderType.Build, site);

            Assert.IsTrue(_world.RunUntil(() => !_world.IsEnabled<ActiveOrder>(dozer), 20f), "the order finished");
            Assert.IsFalse(_world.IsEnabled<MoveDestination>(dozer), "and the walk with it");
            Assert.AreEqual(0f, _world.Get<ConstructionProgress>(site).Value, "nothing was built");
        }

        private void SpawnWall(float3 center, float2 size)
        {
            var wall = _world.EntityManager.CreateEntity();
            _world.EntityManager.AddComponentData(wall, LocalTransform.FromPosition(center));
            var writer = new EntityManagerWriter(_world.EntityManager, wall);
            NavSetup.AddObstacle(ref writer, size);
        }

        private Entity SpawnTruck(float3 position)
        {
            var truck = _world.SpawnUnit(1, position, speed: 8f, radius: TruckRadius);
            _world.EntityManager.SetComponentData(truck, LocalTransform.FromPositionRotation(position,
                quaternion.RotateY(math.PI * 0.5f)));
            _world.EntityManager.SetComponentData(truck, new NavAgent
            {
                Radius = TruckRadius, HullLength = TruckLength, Spacing = UnitAuthoring.DefaultSpacing,
            });
            var writer = new EntityManagerWriter(_world.EntityManager, truck);
            LocomotionAuthoring.Add(ref writer, new Locomotion
            {
                Acceleration = 3f, Braking = 6f, TurnRate = math.radians(35f), TurnRadius = 10f,
                ReverseSpeed = 3f, ReverseDistance = 6f,
            });
            return truck;
        }
    }
}
