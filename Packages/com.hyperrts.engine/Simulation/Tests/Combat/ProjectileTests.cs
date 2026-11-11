using HyperRTS.Simulation.Combat.Damage;
using HyperRTS.Simulation.Combat.Projectiles;
using HyperRTS.Simulation.Combat.Targeting;
using HyperRTS.Simulation.Combat.Weapons;
using HyperRTS.Simulation.Common.Authoring;
using System;
using System.Collections.Generic;
using HyperRTS.Simulation.Common.Geometry;
using HyperRTS.Simulation.Common.Infantry;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.Tests.Combat
{
    /// <summary>Projectiles in flight: ballistic arcs, rocket dives, their kind, and interceptors that shoot them down.</summary>
    public class ProjectileTests : SimulationFixture
    {
        private const int FlightCap = 300;

        /// <summary>One yaw step.</summary>
        private const float YawTolerance = 2f * math.PI / HeadingByte.Steps;

        [Test]
        public void BallisticShell_PeaksAtItsArcHeight()
        {
            SpawnShooter(Trajectory.Ballistic, range: 8f);
            World.SpawnUnit(2, new float3(6f, 0f, 0f));

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
            var enemy = World.SpawnUnit(2, new float3(6f, 0f, 0f));

            var path = Fly(flown =>
            {
                if (flown == 5)
                {
                    World.Set(enemy, LocalTransform.FromPosition(new float3(0f, 0f, 6f)));
                    World.Kill(enemy);
                }
            });

            // The last frame seen is one ground step (a third of a unit) short, still high on the steep descent.
            Assert.Less(math.distance(path[^1].xz, new float2(6f, 0f)), 0.5f, "the aim stayed fixed at launch");
        }

        [Test]
        public void Rocket_ClimbsThenReachesAMovingTarget()
        {
            SpawnShooter(Trajectory.Rocket, range: 12f);
            var enemy = World.SpawnUnit(2, new float3(6f, 0f, 0f));

            var last = float3.zero;
            var path = Fly(_ =>
            {
                last = World.Get<LocalTransform>(enemy).Position + new float3(0.1f, 0f, 0f);
                World.Set(enemy, LocalTransform.FromPosition(last));
            });

            Assert.Greater(path[1].y - path[0].y, math.abs(path[1].x - path[0].x), "it climbs steeply first");
            Assert.Less(math.distance(path[^1], last + new float3(0f, 1f, 0f)), 1f, "it dove onto the target");
            Assert.Less(World.GetHealth(enemy), 100f);
        }

        [TestCase(Trajectory.Ballistic, 0.5f, 4f)]
        [TestCase(Trajectory.Rocket, 0.5f, 3f)]
        [TestCase(Trajectory.Rocket, 0f, 0f)]
        [TestCase(Trajectory.Ballistic, 1f, 0f)]
        public void CoursePresets_KeepTheirCurves(Trajectory trajectory, float t, float height)
        {
            var course = ShotCourse.From(trajectory);
            var start = float3.zero;
            var end = new float3(12f, 0f, 0f);

            var point = course.Point(start, end, 4f, t);

            Assert.AreEqual(height, point.y, 1e-4f);
            Assert.AreEqual(12f * t, point.x, 1e-4f);
            Assert.AreEqual(trajectory == Trajectory.Ballistic, course.PacedOnGround);
        }

        [Test]
        public void Interceptor_KillsTheNearestHostileProjectile_ThenTurnsToTheNext()
        {
            var defence = Defence(Site(), interval: 100f);
            var near = Shot(new float3(-6f, 4f, 0f));
            var far = Shot(new float3(10f, 4f, 0f));

            World.Run(1f);

            Assert.IsFalse(World.EntityManager.Exists(near), "the nearer shot is down");
            Assert.IsTrue(World.EntityManager.Exists(far), "one engagement, one kill");
            Assert.AreEqual(0.5f * math.PI, Pose(defence, 0).YawRadians, YawTolerance, "it turned to the far shot");
            Assert.AreEqual(1, Pose(defence, 0).Shots);
        }

        [Test]
        public void InterceptorMiss_LetsTheProjectileFlyOn()
        {
            var defence = Defence(Site(), killChance: 0f);
            var shot = Shot(new float3(6f, 4f, 0f));

            World.Run(1f);

            Assert.IsTrue(World.EntityManager.Exists(shot));
            Assert.Greater(Pose(defence, 0).Shots, 1, "it kept engaging");
        }

        [Test]
        public void Interceptor_IgnoresOtherKinds_FriendlyShots_AndShotsOutOfRadius()
        {
            var defence = Defence(Site());
            var bomb = Shot(new float3(6f, 4f, 0f), ProjectileKind.Bomb);
            var friendly = Shot(new float3(0f, 4f, 6f), owner: 1);
            var distant = Shot(new float3(0f, 4f, -40f));

            World.Run(1f);

            Assert.IsTrue(World.EntityManager.Exists(bomb));
            Assert.IsTrue(World.EntityManager.Exists(friendly));
            Assert.IsTrue(World.EntityManager.Exists(distant));
            Assert.AreEqual(0, Pose(defence, 0).Shots);
            Assert.AreEqual(0, Pose(defence, 0).Yaw, "nothing tracked, so it stays straight ahead");
        }

        [Test]
        public void Interceptor_TracksAnInboundShotBeforeItIsInRange_ButHoldsFire()
        {
            var defence = Defence(Site());
            Shot(new float3(0f, 4f, -40f), aim: float3.zero);

            World.Run(1f);

            Assert.AreEqual(math.PI, math.abs(Pose(defence, 0).YawRadians), YawTolerance,
                "aimed inside the radius, so the turret is on it");
            Assert.AreEqual(0, Pose(defence, 0).Shots, "out of range, so held fire");
        }

        [Test]
        public void InterceptorOffline_DoesNothing()
        {
            var defence = Defence(World.SpawnBuilding(1, float3.zero, new float2(2f, 2f), new TestBuilding
            {
                Complete = false,
            }));
            var shot = Shot(new float3(6f, 4f, 0f));

            World.Run(1f);

            Assert.IsTrue(World.EntityManager.Exists(shot));
            Assert.AreEqual(0, Pose(defence, 0).Shots);
        }

        [Test]
        public void InterceptorRounds_RunDry_ThenReload()
        {
            var defence = Defence(Site(), killChance: 0f, rounds: 1, reloadTime: 1.5f);
            Shot(new float3(6f, 4f, 0f));

            World.Run(1f);
            Assert.AreEqual(1, Pose(defence, 0).Shots, "one round, then dry");

            World.Run(1f);
            Assert.AreEqual(2, Pose(defence, 0).Shots, "a reloaded round fires");
        }

        [Test]
        public void InterceptorBesideAWeapon_AimsThePoseAfterTheWeapons()
        {
            var tower = Site();
            World.AddWeapon(tower, new TestWeapon { Range = 6f, Cooldown = 100f, Turreted = true });
            Defence(tower, killChance: 0f);
            World.SpawnUnit(2, new float3(4f, 0f, 0f));
            Shot(new float3(-6f, 4f, 0f));

            World.Run(1f);

            Assert.AreEqual(0.5f * math.PI, Pose(tower, 0).YawRadians, YawTolerance, "the gun on the unit");
            Assert.AreEqual(-0.5f * math.PI, Pose(tower, 1).YawRadians, YawTolerance, "the interceptor on the shot");
        }

        [Test]
        public void InterceptorFireRate_FollowsPace()
        {
            var fast = Defence(Site(), killChance: 0f);
            var slow = Defence(World.SpawnBuilding(1, new float3(0f, 0f, 8f), new float2(2f, 2f)), killChance: 0f);
            World.Add(slow, new Pace { Speed = 1f, FireRate = 0.25f, DamageTaken = 1f });
            Shot(new float3(6f, 4f, 4f));

            World.Run(2f);

            Assert.Greater(Pose(fast, 0).Shots, 2 * Pose(slow, 0).Shots);
            Assert.Greater(Pose(slow, 0).Shots, 0);
        }

        private Entity SpawnShooter(Trajectory trajectory, float range) =>
            World.AddWeapon(World.SpawnUnit(1, float3.zero), new TestWeapon
            {
                Range = range,
                Cooldown = 100f,
                Projectile = World.ProjectilePrefab(),
                Trajectory = trajectory,
                ArcHeight = 0.5f,
            });

        /// <summary>Ticks until the one shot has landed, recording its flight; the callback gets the frames flown.</summary>
        private List<float3> Fly(Action<int> beforeTick)
        {
            using var shots = World.Query(typeof(Projectile), typeof(LocalTransform));
            var path = new List<float3>();
            for (var frame = 0; frame < FlightCap; frame++)
            {
                beforeTick(path.Count);
                World.Tick();
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

        private Entity Site() => World.SpawnBuilding(1, float3.zero, new float2(2f, 2f));

        /// <summary>An interceptor weapon: the entity's only one, or after the weapons it has as the bakers lay it out.</summary>
        private Entity Defence(Entity entity, float interval = 0.2f, float killChance = 1f, int rounds = 0,
            float reloadTime = 1f)
        {
            var weapon = new Weapon
            {
                Range = 20f,
                Cooldown = interval,
                SalvoSize = 1,
                Targets = WeaponTargets.Projectile,
                InterceptKinds = ProjectileKind.Shell | ProjectileKind.Rocket | ProjectileKind.Missile,
                KillChance = killChance,
                Aim = new WeaponAim
                {
                    Turreted = true,
                    TraverseRate = math.radians(720f),
                    ElevationRate = math.radians(720f),
                    MinPitch = 0f,
                    MaxPitch = math.radians(85f),
                    AimTolerance = math.radians(2f),
                },
            };
            var mount = WeaponMount.Create(weapon, Ammo.Create(rounds, reloadTime, AmmoRefill.Round));
            if (!World.EntityManager.HasComponent<WeaponMount>(entity))
            {
                WeaponSetup.Add(World.Writer(entity), new[] { mount },
                    CombatStance.Create(Stance.Aggressive, World.Get<LocalTransform>(entity).Position));
                return entity;
            }

            World.Buffer<WeaponMount>(entity).Add(mount);
            World.Buffer<WeaponPose>(entity).Add(default);
            return entity;
        }

        // A slow hostile rocket heading away along its line unless aimed, so it stays where it is for the test.
        private Entity Shot(float3 position, ProjectileKind kind = ProjectileKind.Rocket, byte owner = 2,
            float3? aim = null)
        {
            var shot = World.EntityManager.CreateEntity();
            World.Add(shot, LocalTransform.FromPosition(position));
            World.Add(shot, new Projectile
            {
                Speed = 0.1f,
                Kind = kind,
                Hit = new DamageEvent { Position = aim ?? position * 3f, SourceOwner = owner },
            });
            return shot;
        }

        private WeaponPose Pose(Entity entity, int index) => World.Buffer<WeaponPose>(entity)[index];
    }
}
