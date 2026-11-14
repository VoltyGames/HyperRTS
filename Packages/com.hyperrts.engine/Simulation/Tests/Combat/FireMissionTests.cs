using System.Collections.Generic;
using HyperRTS.Simulation.Combat.Deploy;
using HyperRTS.Simulation.Combat.Projectiles;
using HyperRTS.Simulation.Combat.Targeting;
using HyperRTS.Simulation.Common.Combat;
using HyperRTS.Simulation.Orders;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.Tests.Combat
{
    /// <summary>Fire missions: a launcher that fires only on order drives into range, deploys facing a ground point and lets its whole salvo fly.</summary>
    public class FireMissionTests : SimulationFixture
    {
        private const int SalvoSize = 4;
        private const float Spread = 3f;

        private readonly Dictionary<Entity, float3> _landings = new();

        [SetUp]
        public void Record()
        {
            _landings.Clear();
            World.CheckEveryTick(RecordShots);
        }

        [Test]
        public void FireMission_DrivesIntoRange_DeploysAndFiresOneSalvoAtThePoint_ThenEnds()
        {
            var launcher = Launcher();
            var point = new float3(0f, 0f, 55f);

            Mission(launcher, point);
            World.Tick();
            Assert.IsTrue(World.RunUntil(() => !World.IsEnabled<ActiveOrder>(launcher), 60f), "the order ends");
            World.Run(5f);

            Assert.AreEqual(SalvoSize, _landings.Count, "one full salvo, no more");
            foreach (var landing in _landings.Values)
            {
                Assert.LessOrEqual(math.distance(landing.xz, point.xz), Spread + 1e-3f);
            }

            Assert.Greater(World.Get<LocalTransform>(launcher).Position.z, 10f, "it drove toward the point first");
            Assert.AreEqual(DeployState.Deployed, World.Get<Deployable>(launcher).State, "and stays deployed");
            Assert.IsFalse(World.IsEnabled<PointAim>(launcher));
        }

        [Test]
        public void FiresOnOrderUnit_NeverFiresOnItsOwn_NextToAnEnemy()
        {
            var launcher = Launcher();
            var enemy = World.SpawnUnit(2, new float3(0f, 0f, 20f));
            World.Command(1, new PlayerCommand { Type = CommandType.Deploy, Unit = launcher });

            World.Run(6f);

            Assert.AreEqual(0, _landings.Count, "it fires nothing unordered");
            Assert.AreEqual(100f, World.GetHealth(enemy));
            Assert.IsFalse(World.IsEnabled<AttackTarget>(launcher));
        }

        [Test]
        public void SalvoAtAnEntity_FliesAllItsShots_AfterTheTargetDies()
        {
            var launcher = Launcher(firesOnOrder: false);
            var enemy = World.SpawnUnit(2, new float3(0f, 0f, 20f), new TestUnit { Speed = 0f });
            World.Command(1, new PlayerCommand { Type = CommandType.Deploy, Unit = launcher });
            World.Run(1.5f);
            World.Command(1, new PlayerCommand { Type = CommandType.Attack, Unit = launcher, Target = enemy });

            Assert.IsTrue(World.RunUntil(() => _landings.Count > 0, 5f), "the first shot flew");
            World.Kill(enemy);
            World.Run(6f);

            Assert.AreEqual(SalvoSize, _landings.Count, "the salvo went on without its target");
        }

        [Test]
        public void AttackOrderOnAnEnemy_FiresAMissionAtItsPosition()
        {
            var launcher = Launcher();
            var enemy = World.SpawnUnit(2, new float3(5f, 0f, 30f), new TestUnit { Speed = 0f });

            World.Command(1, new PlayerCommand { Type = CommandType.Attack, Unit = launcher, Target = enemy });
            World.Tick();
            Assert.AreEqual(OrderType.FireMission, World.Get<ActiveOrder>(launcher).Value.Type);

            Assert.IsTrue(World.RunUntil(() => !World.IsEnabled<ActiveOrder>(launcher), 20f));
            World.Run(5f);
            Assert.AreEqual(SalvoSize, _landings.Count);
            foreach (var landing in _landings.Values)
            {
                Assert.LessOrEqual(math.distance(landing.xz, new float2(5f, 30f)), Spread + 1e-3f);
            }
        }

        private void Mission(Entity launcher, float3 point) =>
            World.Command(1, new PlayerCommand { Type = CommandType.FireMission, Unit = launcher, Position = point });

        private Entity Launcher(bool firesOnOrder = true)
        {
            var launcher = World.AddWeapon(World.SpawnUnit(1, float3.zero), new TestWeapon
            {
                Range = 40f,
                MinRange = 10f,
                Damage = 30f,
                Cooldown = 2f,
                Projectile = World.ProjectilePrefab(),
                Trajectory = Trajectory.Ballistic,
                ArcHeight = 0.5f,
                Turreted = true,
            });
            World.AddDeployable(launcher, 1f, 0.6f);
            World.SetSalvo(launcher, SalvoSize, 0.3f);
            var weapon = World.Primary(launcher);
            weapon.Spread = Spread;
            weapon.FiringArc = math.radians(30f);
            World.SetPrimary(launcher, weapon);
            if (firesOnOrder)
            {
                World.SetFiresOnOrder(launcher);
            }

            return launcher;
        }

        /// <summary>Where each projectile aims, as it first appears.</summary>
        private void RecordShots()
        {
            using var shots = World.Query(typeof(Projectile));
            using var entities = shots.ToEntityArray(Allocator.Temp);
            using var flights = shots.ToComponentDataArray<Projectile>(Allocator.Temp);
            for (var i = 0; i < entities.Length; i++)
            {
                _landings.TryAdd(entities[i], flights[i].Hit.Position);
            }
        }
    }
}
