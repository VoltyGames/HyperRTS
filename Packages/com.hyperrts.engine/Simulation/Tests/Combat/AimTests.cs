using HyperRTS.Simulation.Buildings;
using HyperRTS.Simulation.Combat.Projectiles;
using HyperRTS.Simulation.Combat.Targeting;
using HyperRTS.Simulation.Combat.Weapons;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Common.Authoring;
using HyperRTS.Simulation.Navigation.Agents;
using HyperRTS.Simulation.Navigation.Areas;
using HyperRTS.Simulation.Navigation.Drive;
using HyperRTS.Simulation.Movement;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.Tests.Combat
{
    /// <summary>Aiming: replicated weapon poses, turrets traversing before they fire, and hull-fixed guns turning to face.</summary>
    public class AimTests : SimulationFixture
    {
        /// <summary>One yaw step.</summary>
        private const float YawTolerance = 2f * math.PI / HeadingByte.Steps;

        [Test]
        public void ArcingWeapon_PitchesToItsLaunchAngle()
        {
            var gun = World.AddWeapon(World.SpawnUnit(1, float3.zero), new TestWeapon
            {
                Range = 8f,
                Cooldown = 100f,
                Projectile = World.ProjectilePrefab(),
                Trajectory = Trajectory.Ballistic,
                ArcHeight = 0.25f,
            });
            World.SpawnUnit(2, new float3(6f, 0f, 0f));

            World.Run(0.5f);

            // A level shot with an apex of a quarter of the distance leaves at 45 degrees.
            Assert.AreEqual(0.25f * math.PI, Pose(gun, 0).PitchRadians, 0.02f);
        }

        [Test]
        public void TurretedUnit_KeepsItsHeadingWhileFiring_AndSettlesWhenIdle()
        {
            var turret = World.AddWeapon(World.SpawnUnit(1, float3.zero), new TestWeapon
            {
                Range = 6f,
                Cooldown = 0.2f,
                Turreted = true,
            });
            var fixedGun = World.AddWeapon(World.SpawnUnit(1, new float3(0f, 0f, 2f)), new TestWeapon
            {
                Range = 6f,
                Cooldown = 0.2f,
            });
            var enemy = World.SpawnUnit(2, new float3(4f, 0f, 0f), new TestUnit { MaxHealth = 1000f });

            World.Run(1f);

            Assert.Less(World.GetHealth(enemy), 1000f);
            Assert.AreEqual(1f, Forward(turret).z, 1e-3f, "the turreted hull kept facing +Z");
            Assert.Greater(Forward(fixedGun).x, 0.8f, "the fixed gun turned toward the target");

            World.EntityManager.DestroyEntity(enemy);
            World.Run(2f);

            Assert.AreEqual(0, Pose(turret, 0).Yaw, "the idle turret came back to centre");
        }

        [Test]
        public void Turret_FiresOnlyAfterTurningOntoTheTarget_AtItsRate()
        {
            var turret = World.AddWeapon(World.SpawnUnit(1, float3.zero), new TestWeapon
            {
                Range = 6f,
                Cooldown = 0.1f,
                Turreted = true,
                TraverseSpeed = 90f,
            });
            var enemy = World.SpawnUnit(2, new float3(4f, 0f, 0f), new TestUnit { MaxHealth = 1000f });

            World.Run(0.8f);
            Assert.AreEqual(1000f, World.GetHealth(enemy), "still turning a quarter turn at 90 degrees a second");
            Assert.That(Pose(turret, 0).YawAngle, Is.InRange(math.radians(60f), math.radians(80f)));

            World.Run(0.5f);
            Assert.Less(World.GetHealth(enemy), 1000f, "fires once on target");
        }

        [Test]
        public void TraversedTurret_FiresFromItsTurnedMuzzle()
        {
            var turret = World.AddWeapon(World.SpawnUnit(1, float3.zero), new TestWeapon
            {
                Range = 8f,
                Cooldown = 100f,
                Projectile = World.ProjectilePrefab(),
                Turreted = true,
            });
            var weapon = World.Primary(turret);
            weapon.Aim.Muzzle = new float3(0f, 1f, 3f);
            weapon.Aim.HasMuzzle = true;
            weapon.Aim.TurretPivot = new float3(0f, 1f, 0f);
            weapon.Aim.BarrelPivot = weapon.Aim.Muzzle;
            World.SetPrimary(turret, weapon);
            World.SpawnUnit(2, new float3(7f, 0f, 0f));

            using var projectiles = World.Query(typeof(Projectile));
            Assert.IsTrue(World.RunUntil(() => !projectiles.IsEmpty, 2f), "the turret fired");

            // The muzzle 3 m ahead of the turret ring swung a quarter turn to the right.
            var start = projectiles.GetSingleton<Projectile>().Start;
            MathAssert.Near(new float3(3f, 1f, 0f), start, 0.1f);
        }

        [Test]
        public void TurretlessUnit_TurnsAtItsTurnRate_BeforeItFires()
        {
            var gun = SpawnHullGun(float3.zero, 0.5f, 0f, 90f, 4f);
            var enemy = World.SpawnUnit(2, new float3(3f, 0f, 0f));
            var health = World.GetHealth(enemy);

            var elapsed = 0f;
            while (elapsed < 3f && World.GetHealth(enemy) >= health)
            {
                World.Tick();
                elapsed += TestWorld.FrameTime;
                Assert.LessOrEqual(Yaw(gun), elapsed * 90f + 1f, "turned no faster than its turn rate");
            }

            Assert.Less(World.GetHealth(enemy), health, "fired once it faced the target");
            Assert.Greater(elapsed, 0.8f, "held fire while turning a quarter turn at 90 degrees a second");
            Assert.AreEqual(90f, Yaw(gun), 2f, "faced the target when it fired");
        }

        [Test]
        public void LongHull_BesideAWall_DoesNotSwingIntoIt()
        {
            // A wall from x 2.2 to 3.8 along the hull's right side, the target beyond it.
            var wall = World.EntityManager.CreateEntity();
            World.Add(wall, LocalTransform.FromPosition(new float3(3f, 0f, 0f)));
            var writer = World.Writer(wall);
            NavSetup.AddObstacle(writer, new float2(1.6f, 12f));
            World.Tick();

            var gun = SpawnHullGun(float3.zero, 1.6f, 6.9f, 50f, 8f);
            var enemy = World.SpawnUnit(2, new float3(6f, 0f, 0f));
            var agent = World.Get<NavAgent>(gun);
            for (var t = 0f; t < 3f; t += TestWorld.FrameTime)
            {
                World.Tick();
                var transform = World.Get<LocalTransform>(gun);
                var axis = agent.SegmentAxis(transform.Rotation);
                var depth = new NavCapsule(agent.Access(World.Grid()), axis).Depth(transform.Position.xz, axis);
                Assert.AreEqual(0f, depth, 1e-4f, "the hull never swung into the wall");
            }

            Assert.Greater(Yaw(gun), 3f, "turned toward the target until the wall stopped it");
            Assert.Less(Yaw(gun), 80f, "stopped short of facing through the wall");
            Assert.AreEqual(100f, World.GetHealth(enemy), 1e-4f, "never fired off its heading");
        }

        // Faces +z, as spawned, and holds position so it only turns.
        private Entity SpawnHullGun(float3 position, float radius, float hullLength, float turnRate, float range)
        {
            var gun = World.SpawnUnit(1, position, new TestUnit { Radius = radius });
            World.Set(gun, new NavAgent
            {
                Radius = radius, HullLength = hullLength, Mass = 40f, IsVehicle = true, Chassis = new Chassis { Pivots = true },
            });
            var writer = World.Writer(gun);
            LocomotionAuthoring.Add(writer, new Locomotion
            {
                Acceleration = 3f, Braking = 6f, TurnRate = math.radians(turnRate),
            });
            return World.AddWeapon(gun, new TestWeapon { Stance = Stance.HoldPosition, Range = range });
        }

        private float Yaw(Entity unit)
        {
            var forward = math.mul(World.Get<LocalTransform>(unit).Rotation, math.forward());
            return math.degrees(math.atan2(forward.x, forward.z));
        }

        private WeaponPose Pose(Entity entity, int index) => World.Buffer<WeaponPose>(entity)[index];

        private float3 Forward(Entity entity) => World.Get<LocalTransform>(entity).Forward();
    }
}
