using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Navigation;
using HyperRTS.Simulation.Units;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.Tests
{
    /// <summary>Vehicle handling: turning radius, pivots, reversing, braking to the goal and no orbiting.</summary>
    public class LocomotionTests
    {
        private const float TruckRadius = 1.25f;
        private const float TruckTurnRadius = 9f;
        private const float TankRadius = 1.85f;

        private TestWorld _world;

        [SetUp]
        public void SetUp()
        {
            _world = new TestWorld();
            _world.CreateMatch(1, 2);
        }

        [TearDown]
        public void TearDown() => _world.Dispose();

        private Entity SpawnVehicle(float3 position, float radius, float hullLength, Locomotion locomotion)
        {
            var vehicle = _world.SpawnUnit(1, position, speed: 8f, radius: radius);
            _world.EntityManager.SetComponentData(vehicle, new NavAgent
            {
                Radius = radius, HullLength = hullLength, Spacing = UnitAuthoring.DefaultSpacing,
            });
            var writer = new EntityManagerWriter(_world.EntityManager, vehicle);
            LocomotionAuthoring.Add(ref writer, locomotion);
            return vehicle;
        }

        private Entity SpawnTruck(float3 position) => SpawnVehicle(position, TruckRadius, 8f, new Locomotion
        {
            Acceleration = 3f, Braking = 6f, TurnRate = math.radians(40f), TurnRadius = TruckTurnRadius,
            ReverseSpeed = 3f, ReverseDistance = 15f,
        });

        private Entity SpawnTank(float3 position) => SpawnVehicle(position, TankRadius, 6.9f, new Locomotion
        {
            Acceleration = 2.5f, Braking = 6f, TurnRate = math.radians(50f), Pivots = true,
            ReverseSpeed = 3f, ReverseDistance = 25f,
        });

        // Ticks once so the order is under way before callers wait on arrival.
        private void Order(Entity unit, float3 goal)
        {
            _world.MoveTo(unit, goal);
            _world.Tick();
        }

        private float2 Facing(Entity unit) =>
            math.normalize(math.mul(_world.Get<LocalTransform>(unit).Rotation, math.forward()).xz);

        private float Speed(Entity unit) => _world.Get<LocomotionState>(unit).Speed;

        private bool Arrived(Entity unit) => !_world.IsEnabled<MoveDestination>(unit);

        private static float TurnBetween(float2 a, float2 b) => math.acos(math.clamp(math.dot(a, b), -1f, 1f));

        [Test]
        public void WheeledTruck_NeverTurnsTighterThanItsRadius()
        {
            var truck = SpawnTruck(float3.zero);
            Order(truck, new float3(40f, 0f, 20f));

            var tightest = float.MaxValue;
            for (var t = 0f; t < 30f && !Arrived(truck); t += TestWorld.FrameTime)
            {
                var facing = Facing(truck);
                var position = _world.GetPosition(truck).xz;
                _world.Tick();
                var turn = TurnBetween(facing, Facing(truck));
                if (turn > 1e-4f)
                {
                    tightest = math.min(tightest, math.distance(position, _world.GetPosition(truck).xz) / turn);
                }
            }

            Assert.IsTrue(Arrived(truck), "arrived");
            Assert.GreaterOrEqual(tightest, TruckTurnRadius * 0.95f, "arcs no tighter than the turning radius");
        }

        [Test]
        public void TrackedTank_PivotsInPlace_ForAGoalFarBehind()
        {
            var tank = SpawnTank(float3.zero);
            Order(tank, new float3(-40f, 0f, -10f));
            _world.Run(1f);

            Assert.Less(math.length(_world.GetPosition(tank).xz), 0.3f, "turns standing");
            Assert.Greater(TurnBetween(new float2(0f, 1f), Facing(tank)), math.radians(40f), "turned at its rate");
            Assert.IsTrue(_world.RunUntil(() => Arrived(tank), 20f), "then drives there");
        }

        [Test]
        public void Tank_BacksUpToAGoalCloseBehind()
        {
            var tank = SpawnTank(float3.zero);
            var goal = new float3(0f, 0f, -10f);
            Order(tank, goal);

            var slowest = 0f;
            for (var t = 0f; t < 15f && !Arrived(tank); t += TestWorld.FrameTime)
            {
                _world.Tick();
                slowest = math.min(slowest, Speed(tank));
            }

            Assert.IsTrue(Arrived(tank), "arrived");
            Assert.Less(slowest, -2f, "reversed");
            Assert.Greater(math.dot(Facing(tank), new float2(0f, 1f)), 0.98f, "never turned round");
            Assert.Less(math.distance(_world.GetPosition(tank).xz, goal.xz), NavTolerances.Arrive(TankRadius) + 0.1f);
        }

        [Test]
        public void Truck_Accelerates_ThenBrakesToStopAtTheGoal()
        {
            var truck = SpawnTruck(float3.zero);
            var goal = new float3(0f, 0f, 40f);
            Order(truck, goal);
            _world.Run(1f);
            Assert.That(Speed(truck), Is.InRange(2.5f, 3.2f), "gains its acceleration each second");

            Assert.IsTrue(_world.RunUntil(() => Arrived(truck), 20f), "arrived");
            _world.Run(0.5f);
            Assert.AreEqual(0f, Speed(truck), "stopped");
            Assert.Less(math.distance(_world.GetPosition(truck).xz, goal.xz), NavTolerances.Arrive(TruckRadius) + 0.1f);
        }

        [Test]
        public void Truck_GoalInsideItsTurningCircle_ShuntsInsteadOfOrbiting()
        {
            var truck = SpawnTruck(float3.zero);
            var goal = new float3(3f, 0f, 4f);
            Order(truck, goal);

            var turned = 0f;
            var slowest = 0f;
            for (var t = 0f; t < 30f && !Arrived(truck); t += TestWorld.FrameTime)
            {
                var facing = Facing(truck);
                _world.Tick();
                turned += TurnBetween(facing, Facing(truck));
                slowest = math.min(slowest, Speed(truck));
            }

            Assert.IsTrue(Arrived(truck), "arrived");
            Assert.Less(slowest, 0f, "backed up to bring the goal out of its turning circle");
            Assert.Less(turned, math.PI * 1.5f, "never circled");
            Assert.Less(math.distance(_world.GetPosition(truck).xz, goal.xz), TruckRadius + 0.1f);
        }

        [Test]
        public void Truck_ShuntingIntoAWall_DrivesTheOtherLegAndStillArrives()
        {
            var wall = _world.EntityManager.CreateEntity();
            _world.EntityManager.AddComponentData(wall, LocalTransform.FromPosition(new float3(0f, 0f, -6f)));
            var writer = new EntityManagerWriter(_world.EntityManager, wall);
            NavSetup.AddObstacle(ref writer, new float2(16f, 1f));
            _world.Tick();

            var truck = SpawnTruck(float3.zero);
            var goal = new float3(3f, 0f, 4f);
            Order(truck, goal);

            Assert.IsTrue(_world.RunUntil(() => Arrived(truck), 30f), "arrived");
            Assert.Less(math.distance(_world.GetPosition(truck).xz, goal.xz), TruckRadius + 0.1f);
        }

        [Test]
        public void Shove_MovesTheHull_ButNeverTurnsIt()
        {
            var tank = SpawnTank(float3.zero);
            _world.SpawnUnit(1, new float3(1.5f, 0f, 0.5f));
            _world.Run(0.5f);

            Assert.Greater(math.length(_world.GetPosition(tank).xz), 0.05f, "pushed aside");
            Assert.AreEqual(1f, math.dot(Facing(tank), new float2(0f, 1f)), 1e-5f, "facing kept");
        }

        [Test]
        public void Infantry_StepsAtFullSpeedAndFacesItsGoal_AtOnce()
        {
            var soldier = _world.SpawnUnit(1, float3.zero, speed: 5f, radius: 0.4f);
            Order(soldier, new float3(10f, 0f, 0f));
            _world.Tick(TestWorld.FrameTime, 2);

            var before = _world.GetPosition(soldier).xz;
            _world.Tick();

            Assert.AreEqual(5f * TestWorld.FrameTime, math.distance(before, _world.GetPosition(soldier).xz), 1e-4f);
            Assert.AreEqual(1f, math.dot(Facing(soldier), new float2(1f, 0f)), 1e-4f);
            Assert.IsFalse(_world.EntityManager.HasComponent<LocomotionState>(soldier));
        }
    }
}
