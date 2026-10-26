using HyperRTS.Simulation.Buildings;
using HyperRTS.Simulation.Combat;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Navigation;
using HyperRTS.Simulation.Units;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.Tests.Combat
{
    /// <summary>Aiming: replicated weapon poses, turrets traversing before they fire, and hull-fixed guns turning to face.</summary>
    public class AimTests
    {
        /// <summary>One yaw step.</summary>
        private const float YawTolerance = 2f * math.PI / HeadingByte.Steps;

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
        public void ArcingWeapon_PitchesToItsLaunchAngle()
        {
            var gun = _world.AddWeapon(_world.SpawnUnit(1, float3.zero), range: 8f, cooldown: 100f,
                projectile: _world.ProjectilePrefab(), trajectory: Trajectory.Ballistic, arcHeight: 0.25f);
            _world.SpawnUnit(2, new float3(6f, 0f, 0f));

            _world.Run(0.5f);

            // A level shot with an apex of a quarter of the distance leaves at 45 degrees.
            Assert.AreEqual(0.25f * math.PI, Pose(gun, 0).PitchRadians, 0.02f);
        }

        [Test]
        public void Shots_CountEachShot()
        {
            var gun = _world.AddWeapon(_world.SpawnUnit(1, float3.zero), range: 6f, damage: 10f, cooldown: 0.4f);
            var enemy = _world.SpawnUnit(2, new float3(4f, 0f, 0f), maxHealth: 1000f);

            _world.Run(2f);

            var shots = (int)math.round((1000f - _world.GetHealth(enemy)) / 10f);
            Assert.Greater(shots, 1);
            Assert.AreEqual(shots, Pose(gun, 0).Shots);
        }

        [Test]
        public void TurretedUnit_KeepsItsHeadingWhileFiring_AndSettlesWhenIdle()
        {
            var turret = _world.AddWeapon(_world.SpawnUnit(1, float3.zero), range: 6f, cooldown: 0.2f, turreted: true);
            var fixedGun = _world.AddWeapon(_world.SpawnUnit(1, new float3(0f, 0f, 2f)), range: 6f, cooldown: 0.2f);
            var enemy = _world.SpawnUnit(2, new float3(4f, 0f, 0f), maxHealth: 1000f);

            _world.Run(1f);

            Assert.Less(_world.GetHealth(enemy), 1000f);
            Assert.AreEqual(1f, Forward(turret).z, 1e-3f, "the turreted hull kept facing +Z");
            Assert.Greater(Forward(fixedGun).x, 0.8f, "the fixed gun turned toward the target");

            _world.EntityManager.DestroyEntity(enemy);
            _world.Run(2f);

            Assert.AreEqual(0, Pose(turret, 0).Yaw, "the idle turret came back to centre");
        }

        [Test]
        public void Turret_FiresOnlyAfterTurningOntoTheTarget_AtItsRate()
        {
            var turret = _world.AddWeapon(_world.SpawnUnit(1, float3.zero), range: 6f, cooldown: 0.1f, turreted: true,
                traverseSpeed: 90f);
            var enemy = _world.SpawnUnit(2, new float3(4f, 0f, 0f), maxHealth: 1000f);

            _world.Run(0.8f);
            Assert.AreEqual(1000f, _world.GetHealth(enemy), "still turning a quarter turn at 90 degrees a second");
            Assert.That(Pose(turret, 0).YawAngle, Is.InRange(math.radians(60f), math.radians(80f)));

            _world.Run(0.5f);
            Assert.Less(_world.GetHealth(enemy), 1000f, "fires once on target");
        }

        [Test]
        public void TraversedTurret_FiresFromItsTurnedMuzzle()
        {
            var turret = _world.AddWeapon(_world.SpawnUnit(1, float3.zero), range: 8f, cooldown: 100f,
                projectile: _world.ProjectilePrefab(), turreted: true);
            var weapon = _world.Get<Weapon>(turret);
            weapon.Muzzle = new float3(0f, 1f, 3f);
            weapon.HasMuzzle = true;
            weapon.TurretPivot = new float3(0f, 1f, 0f);
            weapon.BarrelPivot = weapon.Muzzle;
            _world.EntityManager.SetComponentData(turret, weapon);
            _world.SpawnUnit(2, new float3(7f, 0f, 0f));

            using var projectiles = _world.EntityManager.CreateEntityQuery(typeof(Projectile));
            Assert.IsTrue(_world.RunUntil(() => !projectiles.IsEmpty, 2f), "the turret fired");

            // The muzzle 3 m ahead of the turret ring swung a quarter turn to the right.
            var start = projectiles.GetSingleton<Projectile>().Start;
            MathAssert.Near(new float3(3f, 1f, 0f), start, 0.1f);
        }

        [Test]
        public void BuildingWeapon_AimsItsTurret_AndFiresOnceOnTarget()
        {
            var model = new UnityEngine.GameObject("Tower");
            try
            {
                var authoring = model.AddComponent<WeaponAuthoring>();
                model.AddComponent<BuildingAuthoring>();
                Assert.IsTrue(authoring.AimsTurret, "a building never turns, so its weapon aims a turret");

                var tower = _world.AddWeapon(_world.SpawnBuilding(1, float3.zero, new float2(2f, 2f)), range: 6f,
                    cooldown: 0.1f, turreted: authoring.AimsTurret, traverseSpeed: 90f);
                var enemy = _world.SpawnUnit(2, new float3(-4f, 0f, 0f), maxHealth: 1000f);

                _world.Run(1.5f);
                Assert.AreEqual(-0.5f * math.PI, Pose(tower, 0).YawRadians, YawTolerance);
                Assert.Less(_world.GetHealth(enemy), 1000f);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(model);
            }
        }

        [Test]
        public void TurretlessUnit_TurnsAtItsTurnRate_BeforeItFires()
        {
            var gun = SpawnHullGun(float3.zero, 0.5f, 0f, 90f, 4f);
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

            var gun = SpawnHullGun(float3.zero, 1.6f, 6.9f, 50f, 8f);
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

        // Faces +z, as spawned, and holds position so it only turns.
        private Entity SpawnHullGun(float3 position, float radius, float hullLength, float turnRate, float range)
        {
            var gun = _world.SpawnUnit(1, position, radius: radius);
            _world.EntityManager.SetComponentData(gun, new NavAgent
            {
                Radius = radius, HullLength = hullLength, Chassis = new Chassis { Pivots = true },
            });
            var writer = new EntityManagerWriter(_world.EntityManager, gun);
            LocomotionAuthoring.Add(ref writer, new Locomotion
            {
                Acceleration = 3f, Braking = 6f, TurnRate = math.radians(turnRate),
            });
            return _world.AddWeapon(gun, Stance.HoldPosition, range: range);
        }

        private float Yaw(Entity unit)
        {
            var forward = math.mul(_world.Get<LocalTransform>(unit).Rotation, math.forward());
            return math.degrees(math.atan2(forward.x, forward.z));
        }

        private WeaponPose Pose(Entity entity, int index) => _world.EntityManager.GetBuffer<WeaponPose>(entity)[index];

        private float3 Forward(Entity entity) => _world.Get<LocalTransform>(entity).Forward();
    }
}
