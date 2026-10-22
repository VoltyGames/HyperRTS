using HyperRTS.Simulation.Combat;
using HyperRTS.Simulation.Common;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.Tests
{
    /// <summary>Interceptors: target choice, kills and misses, engaged kinds, ammo, offline and pose order.</summary>
    public class InterceptorTests
    {
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
        public void KillsTheNearestHostileProjectile_ThenTurnsToTheNext()
        {
            var defence = Defence(_world.SpawnBuilding(1, float3.zero, new float2(2f, 2f)), interval: 100f);
            var near = Shot(new float3(-6f, 4f, 0f));
            var far = Shot(new float3(10f, 4f, 0f));

            _world.Run(1f);

            Assert.IsFalse(_world.EntityManager.Exists(near), "the nearer shot is down");
            Assert.IsTrue(_world.EntityManager.Exists(far), "one engagement, one kill");
            Assert.AreEqual(far, _world.Get<Interceptor>(defence).Target);
            Assert.AreEqual(0.5f * math.PI, Pose(defence, 0).YawRadians, YawTolerance, "it turned to the far shot");
            Assert.AreEqual(1, Pose(defence, 0).Shots);
        }

        [Test]
        public void Miss_LetsTheProjectileFlyOn()
        {
            var defence = Defence(_world.SpawnBuilding(1, float3.zero, new float2(2f, 2f)), killChance: 0f);
            var shot = Shot(new float3(6f, 4f, 0f));

            _world.Run(1f);

            Assert.IsTrue(_world.EntityManager.Exists(shot));
            Assert.Greater(Pose(defence, 0).Shots, 1, "it kept engaging");
        }

        [Test]
        public void IgnoresOtherKinds_FriendlyShots_AndShotsOutOfRadius()
        {
            var defence = Defence(_world.SpawnBuilding(1, float3.zero, new float2(2f, 2f)));
            var bomb = Shot(new float3(6f, 4f, 0f), ProjectileKind.Bomb);
            var friendly = Shot(new float3(0f, 4f, 6f), faction: 1);
            var distant = Shot(new float3(0f, 4f, -40f));

            _world.Run(1f);

            Assert.IsTrue(_world.EntityManager.Exists(bomb));
            Assert.IsTrue(_world.EntityManager.Exists(friendly));
            Assert.IsTrue(_world.EntityManager.Exists(distant));
            Assert.AreEqual(Entity.Null, _world.Get<Interceptor>(defence).Target);
        }

        [Test]
        public void Offline_DoesNothing()
        {
            var site = _world.SpawnBuilding(1, float3.zero, new float2(2f, 2f), complete: false);
            var defence = Defence(site);
            var shot = Shot(new float3(6f, 4f, 0f));

            _world.Run(1f);

            Assert.IsTrue(_world.EntityManager.Exists(shot));
            Assert.AreEqual(0, Pose(defence, 0).Shots);
        }

        [Test]
        public void Rounds_RunDry_ThenReload()
        {
            var defence = Defence(_world.SpawnBuilding(1, float3.zero, new float2(2f, 2f)), killChance: 0f,
                rounds: 1, reloadTime: 1.5f);
            Shot(new float3(6f, 4f, 0f));

            _world.Run(1f);
            Assert.AreEqual(1, Pose(defence, 0).Shots, "one round, then dry");

            _world.Run(1f);
            Assert.AreEqual(2, Pose(defence, 0).Shots, "a reloaded round fires");
        }

        [Test]
        public void BesideAWeapon_ItAimsThePoseAfterTheWeapons()
        {
            var tower = _world.SpawnBuilding(1, float3.zero, new float2(2f, 2f));
            _world.AddWeapon(tower, range: 6f, cooldown: 100f, turreted: true);
            _world.EntityManager.GetBuffer<WeaponPose>(tower).Add(default);
            Defence(tower, killChance: 0f, pose: 1);
            _world.SpawnUnit(2, new float3(4f, 0f, 0f));
            Shot(new float3(-6f, 4f, 0f));

            _world.Run(1f);

            Assert.AreEqual(0.5f * math.PI, Pose(tower, 0).YawRadians, YawTolerance, "the gun on the unit");
            Assert.AreEqual(-0.5f * math.PI, Pose(tower, 1).YawRadians, YawTolerance, "the interceptor on the shot");
        }

        private Entity Defence(Entity entity, float interval = 0.2f, float killChance = 1f, int rounds = 0,
            float reloadTime = 1f, byte pose = 0)
        {
            var interceptor = new Interceptor
            {
                Radius = 20f,
                Interval = interval,
                KillChance = killChance,
                Kinds = ProjectileKind.Shell | ProjectileKind.Rocket | ProjectileKind.Missile,
                Aim = new Weapon
                {
                    Turreted = true,
                    TraverseRate = math.radians(720f),
                    ElevationRate = math.radians(720f),
                    MinPitch = 0f,
                    MaxPitch = math.radians(85f),
                    AimTolerance = math.radians(2f),
                },
                Pose = pose,
                Rounds = rounds,
                MaxRounds = rounds,
                ReloadTime = reloadTime,
            };
            var writer = new EntityManagerWriter(_world.EntityManager, entity);
            InterceptorAuthoring.Add(ref writer, interceptor);
            return entity;
        }

        // A slow hostile rocket heading away along its line, so it stays in range for the test.
        private Entity Shot(float3 position, ProjectileKind kind = ProjectileKind.Rocket, byte faction = 2)
        {
            var shot = _world.EntityManager.CreateEntity();
            _world.EntityManager.AddComponentData(shot, LocalTransform.FromPosition(position));
            _world.EntityManager.AddComponentData(shot, new Projectile
            {
                Speed = 0.1f,
                Kind = kind,
                Hit = new DamageEvent { Position = position * 3f, SourceFaction = faction },
            });
            return shot;
        }

        private WeaponPose Pose(Entity entity, int index) => _world.EntityManager.GetBuffer<WeaponPose>(entity)[index];
    }
}
