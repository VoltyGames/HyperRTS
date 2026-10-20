using HyperRTS.Simulation.Combat;
using HyperRTS.Simulation.Common;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.Tests
{
    /// <summary>Replicated weapon poses: aim per weapon, launch angles, shot counts, settling and turreted hulls.</summary>
    public class WeaponPoseTests
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
        public void PrimaryAndIndependentMount_EachAimAtTheirOwnTarget()
        {
            var gun = _world.AddWeapon(_world.SpawnUnit(1, float3.zero), range: 6f, cooldown: 100f, turreted: true);
            _world.AddMount(gun, range: 6f, cooldown: 100f, targets: WeaponTargets.Air, independent: true);
            _world.SpawnUnit(2, new float3(-3f, 0f, 0f));
            _world.SpawnAircraft(2, new float3(3f, 0f, 0f));

            _world.Run(1f);

            Assert.AreEqual(-0.5f * math.PI, Pose(gun, 0).YawRadians, YawTolerance, "the primary looks left");
            Assert.AreEqual(0f, Pose(gun, 0).PitchRadians, 0.02f, "level with a ground target");
            Assert.AreEqual(0.5f * math.PI, Pose(gun, 1).YawRadians, YawTolerance, "the mount looks right");
        }

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

        private WeaponPose Pose(Entity entity, int index) => _world.EntityManager.GetBuffer<WeaponPose>(entity)[index];

        private float3 Forward(Entity entity) => _world.Get<LocalTransform>(entity).Forward();
    }
}
