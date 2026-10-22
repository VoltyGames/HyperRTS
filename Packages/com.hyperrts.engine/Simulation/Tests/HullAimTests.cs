using HyperRTS.Simulation.Combat;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Navigation;
using HyperRTS.Simulation.Units;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.Tests
{
    /// <summary>Units with hull-fixed weapons turning through movement to aim, and firing only once they face.</summary>
    public class HullAimTests
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

        // Faces +z, as spawned, and holds position so it only turns.
        private Entity SpawnGun(float3 position, float radius, float hullLength, float turnRate, float range)
        {
            var gun = _world.SpawnUnit(1, position, radius: radius);
            _world.EntityManager.SetComponentData(gun, new NavAgent { Radius = radius, HullLength = hullLength });
            var writer = new EntityManagerWriter(_world.EntityManager, gun);
            LocomotionAuthoring.Add(ref writer, new Locomotion
            {
                Acceleration = 3f, Braking = 6f, TurnRate = math.radians(turnRate), Pivots = true,
            });
            return _world.AddWeapon(gun, Stance.HoldPosition, range: range);
        }

        private float Yaw(Entity unit)
        {
            var forward = math.mul(_world.Get<LocalTransform>(unit).Rotation, math.forward());
            return math.degrees(math.atan2(forward.x, forward.z));
        }

        [Test]
        public void TurretlessUnit_TurnsAtItsTurnRate_BeforeItFires()
        {
            var gun = SpawnGun(float3.zero, 0.5f, 0f, 90f, 4f);
            var enemy = _world.SpawnUnit(2, new float3(3f, 0f, 0f));
            var health = _world.GetHealth(enemy);

            var elapsed = 0f;
            while (elapsed < 3f && _world.GetHealth(enemy) >= health)
            {
                _world.Tick();
                elapsed += TestWorld.FrameTime;
                Assert.LessOrEqual(Yaw(gun), elapsed * 90f + 1f, "turned no faster than its turn rate");
            }

            Assert.Less(_world.GetHealth(enemy), health, "fired once it faced the target");
            Assert.Greater(elapsed, 0.8f, "held fire while turning a quarter turn at 90 degrees a second");
            Assert.AreEqual(90f, Yaw(gun), 2f, "faced the target when it fired");
        }

        [Test]
        public void LongHull_BesideAWall_DoesNotSwingIntoIt()
        {
            // A wall from x 2.2 to 3.8 along the hull's right side, the target beyond it.
            var wall = _world.EntityManager.CreateEntity();
            _world.EntityManager.AddComponentData(wall, LocalTransform.FromPosition(new float3(3f, 0f, 0f)));
            var writer = new EntityManagerWriter(_world.EntityManager, wall);
            NavSetup.AddObstacle(ref writer, new float2(1.6f, 12f));
            _world.Tick();

            var gun = SpawnGun(float3.zero, 1.6f, 6.9f, 50f, 8f);
            var enemy = _world.SpawnUnit(2, new float3(6f, 0f, 0f));
            var agent = _world.Get<NavAgent>(gun);
            for (var t = 0f; t < 3f; t += TestWorld.FrameTime)
            {
                _world.Tick();
                var transform = _world.Get<LocalTransform>(gun);
                var axis = agent.SegmentAxis(transform.Rotation);
                var depth = new NavCapsule(agent.Access(_world.Grid()), axis).Depth(transform.Position.xz, axis);
                Assert.AreEqual(0f, depth, 1e-4f, "the hull never swung into the wall");
            }

            Assert.Greater(Yaw(gun), 3f, "turned toward the target until the wall stopped it");
            Assert.Less(Yaw(gun), 80f, "stopped short of facing through the wall");
            Assert.AreEqual(100f, _world.GetHealth(enemy), 1e-4f, "never fired off its heading");
        }
    }
}
