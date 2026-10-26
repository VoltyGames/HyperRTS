using System;
using System.Collections.Generic;
using HyperRTS.Simulation.Combat;
using HyperRTS.Simulation.Common;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.Tests.Combat
{
    /// <summary>Projectiles in flight: ballistic arcs, rocket dives, their kind, and interceptors that shoot them down.</summary>
    public class ProjectileTests
    {
        private const int FlightCap = 300;

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
        public void BallisticShell_PeaksAtItsArcHeight()
        {
            SpawnShooter(Trajectory.Ballistic, range: 8f);
            _world.SpawnUnit(2, new float3(6f, 0f, 0f));

            var peak = float.MinValue;
            foreach (var point in Fly(_ => { }))
            {
                peak = math.max(peak, point.y);
            }

            // Launched and aimed 1 above the pivots, 6 apart, so the apex is 0.5 * 6 above that.
            Assert.AreEqual(4f, peak, 0.05f);
        }

        [Test]
        public void BallisticShell_LandsWhereTheTargetStood_EvenIfItMovedAndDied()
        {
            SpawnShooter(Trajectory.Ballistic, range: 8f);
            var enemy = _world.SpawnUnit(2, new float3(6f, 0f, 0f));

            var path = Fly(flown =>
            {
                if (flown == 5)
                {
                    _world.EntityManager.SetComponentData(enemy, LocalTransform.FromPosition(new float3(0f, 0f, 6f)));
                    _world.Kill(enemy);
                }
            });

            // The last frame seen is one ground step (a third of a unit) short, still high on the steep descent.
            Assert.Less(math.distance(path[^1].xz, new float2(6f, 0f)), 0.5f, "the aim stayed fixed at launch");
        }

        [Test]
        public void BallisticShell_MissesATargetThatMovedAway()
        {
            SpawnShooter(Trajectory.Ballistic, range: 8f);
            var enemy = _world.SpawnUnit(2, new float3(6f, 0f, 0f));

            Fly(flown =>
            {
                if (flown == 5)
                {
                    _world.EntityManager.SetComponentData(enemy, LocalTransform.FromPosition(new float3(6f, 0f, 3f)));
                }
            });

            Assert.AreEqual(100f, _world.GetHealth(enemy));
        }

        [Test]
        public void Rocket_ClimbsThenReachesAMovingTarget()
        {
            SpawnShooter(Trajectory.Rocket, range: 12f);
            var enemy = _world.SpawnUnit(2, new float3(6f, 0f, 0f));

            var last = float3.zero;
            var path = Fly(_ =>
            {
                last = _world.Get<LocalTransform>(enemy).Position + new float3(0.1f, 0f, 0f);
                _world.EntityManager.SetComponentData(enemy, LocalTransform.FromPosition(last));
            });

            Assert.Greater(path[1].y - path[0].y, math.abs(path[1].x - path[0].x), "it climbs steeply first");
            Assert.Less(math.distance(path[^1], last + new float3(0f, 1f, 0f)), 1f, "it dove onto the target");
            Assert.Less(_world.GetHealth(enemy), 100f);
        }

        [Test]
        public void Launch_GivesTheProjectileItsWeaponsKind()
        {
            var gun = SpawnShooter(Trajectory.Rocket, range: 8f);
            var weapon = _world.Get<Weapon>(gun);
            weapon.ProjectileKind = ProjectileKind.Rocket;
            _world.EntityManager.SetComponentData(gun, weapon);
            _world.SpawnUnit(2, new float3(7f, 0f, 0f));

            using var projectiles = _world.EntityManager.CreateEntityQuery(typeof(Projectile));
            Assert.IsTrue(_world.RunUntil(() => !projectiles.IsEmpty, 2f), "the gun fired");
            Assert.AreEqual(ProjectileKind.Rocket, projectiles.GetSingleton<Projectile>().Kind);
        }

        [Test]
        public void Interceptor_KillsTheNearestHostileProjectile_ThenTurnsToTheNext()
        {
            var defence = Defence(Site(), interval: 100f);
            var near = Shot(new float3(-6f, 4f, 0f));
            var far = Shot(new float3(10f, 4f, 0f));

            _world.Run(1f);

            Assert.IsFalse(_world.EntityManager.Exists(near), "the nearer shot is down");
            Assert.IsTrue(_world.EntityManager.Exists(far), "one engagement, one kill");
            Assert.AreEqual(0.5f * math.PI, Pose(defence, 0).YawRadians, YawTolerance, "it turned to the far shot");
            Assert.AreEqual(1, Pose(defence, 0).Shots);
        }

        [Test]
        public void InterceptorMiss_LetsTheProjectileFlyOn()
        {
            var defence = Defence(Site(), killChance: 0f);
            var shot = Shot(new float3(6f, 4f, 0f));

            _world.Run(1f);

            Assert.IsTrue(_world.EntityManager.Exists(shot));
            Assert.Greater(Pose(defence, 0).Shots, 1, "it kept engaging");
        }

        [Test]
        public void Interceptor_IgnoresOtherKinds_FriendlyShots_AndShotsOutOfRadius()
        {
            var defence = Defence(Site());
            var bomb = Shot(new float3(6f, 4f, 0f), ProjectileKind.Bomb);
            var friendly = Shot(new float3(0f, 4f, 6f), faction: 1);
            var distant = Shot(new float3(0f, 4f, -40f));

            _world.Run(1f);

            Assert.IsTrue(_world.EntityManager.Exists(bomb));
            Assert.IsTrue(_world.EntityManager.Exists(friendly));
            Assert.IsTrue(_world.EntityManager.Exists(distant));
            Assert.AreEqual(0, Pose(defence, 0).Shots);
            Assert.AreEqual(0, Pose(defence, 0).Yaw, "nothing tracked, so it stays straight ahead");
        }

        [Test]
        public void Interceptor_TracksAnInboundShotBeforeItIsInRange_ButHoldsFire()
        {
            var defence = Defence(Site());
            Shot(new float3(0f, 4f, -40f), aim: float3.zero);

            _world.Run(1f);

            Assert.AreEqual(math.PI, math.abs(Pose(defence, 0).YawRadians), YawTolerance,
                "aimed inside the radius, so the turret is on it");
            Assert.AreEqual(0, Pose(defence, 0).Shots, "out of range, so held fire");
        }

        [Test]
        public void InterceptorOffline_DoesNothing()
        {
            var defence = Defence(_world.SpawnBuilding(1, float3.zero, new float2(2f, 2f), complete: false));
            var shot = Shot(new float3(6f, 4f, 0f));

            _world.Run(1f);

            Assert.IsTrue(_world.EntityManager.Exists(shot));
            Assert.AreEqual(0, Pose(defence, 0).Shots);
        }

        [Test]
        public void InterceptorRounds_RunDry_ThenReload()
        {
            var defence = Defence(Site(), killChance: 0f, rounds: 1, reloadTime: 1.5f);
            Shot(new float3(6f, 4f, 0f));

            _world.Run(1f);
            Assert.AreEqual(1, Pose(defence, 0).Shots, "one round, then dry");

            _world.Run(1f);
            Assert.AreEqual(2, Pose(defence, 0).Shots, "a reloaded round fires");
        }

        [Test]
        public void InterceptorBesideAWeapon_AimsThePoseAfterTheWeapons()
        {
            var tower = Site();
            _world.AddWeapon(tower, range: 6f, cooldown: 100f, turreted: true);
            _world.EntityManager.GetBuffer<WeaponPose>(tower).Add(default);
            Defence(tower, killChance: 0f, pose: 1);
            _world.SpawnUnit(2, new float3(4f, 0f, 0f));
            Shot(new float3(-6f, 4f, 0f));

            _world.Run(1f);

            Assert.AreEqual(0.5f * math.PI, Pose(tower, 0).YawRadians, YawTolerance, "the gun on the unit");
            Assert.AreEqual(-0.5f * math.PI, Pose(tower, 1).YawRadians, YawTolerance, "the interceptor on the shot");
        }

        private Entity SpawnShooter(Trajectory trajectory, float range) =>
            _world.AddWeapon(_world.SpawnUnit(1, float3.zero), range: range, cooldown: 100f,
                projectile: _world.ProjectilePrefab(), trajectory: trajectory, arcHeight: 0.5f);

        /// <summary>Ticks until the one shot has landed, recording its flight; the callback gets the frames flown.</summary>
        private List<float3> Fly(Action<int> beforeTick)
        {
            using var shots = _world.EntityManager.CreateEntityQuery(typeof(Projectile), typeof(LocalTransform));
            var path = new List<float3>();
            for (var frame = 0; frame < FlightCap; frame++)
            {
                beforeTick(path.Count);
                _world.Tick();
                if (shots.IsEmpty)
                {
                    if (path.Count > 0)
                    {
                        return path;
                    }

                    continue;
                }

                path.Add(shots.GetSingleton<LocalTransform>().Position);
            }

            Assert.Fail("the shot never landed");
            return path;
        }

        private Entity Site() => _world.SpawnBuilding(1, float3.zero, new float2(2f, 2f));

        private Entity Defence(Entity entity, float interval = 0.2f, float killChance = 1f, int rounds = 0,
            float reloadTime = 1f, byte pose = 0)
        {
            var interceptor = new Interceptor
            {
                Radius = 20f,
                KillChance = killChance,
                Kinds = ProjectileKind.Shell | ProjectileKind.Rocket | ProjectileKind.Missile,
                Aim = new Weapon
                {
                    Cooldown = interval,
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

        // A slow hostile rocket heading away along its line unless aimed, so it stays where it is for the test.
        private Entity Shot(float3 position, ProjectileKind kind = ProjectileKind.Rocket, byte faction = 2,
            float3? aim = null)
        {
            var shot = _world.EntityManager.CreateEntity();
            _world.EntityManager.AddComponentData(shot, LocalTransform.FromPosition(position));
            _world.EntityManager.AddComponentData(shot, new Projectile
            {
                Speed = 0.1f,
                Kind = kind,
                Hit = new DamageEvent { Position = aim ?? position * 3f, SourceFaction = faction },
            });
            return shot;
        }

        private WeaponPose Pose(Entity entity, int index) => _world.EntityManager.GetBuffer<WeaponPose>(entity)[index];
    }
}
