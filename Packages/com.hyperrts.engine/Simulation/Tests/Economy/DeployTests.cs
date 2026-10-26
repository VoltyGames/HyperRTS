using HyperRTS.Simulation.Combat;
using HyperRTS.Simulation.Orders;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.Tests.Economy
{
    /// <summary>
    /// Deployable units: deploy and pack-up timing, fire and turret held while packed, moves refused while rooted.
    /// </summary>
    public class DeployTests
    {
        private const float DeployTime = 1f;
        private const float PackUpTime = 0.6f;

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
        public void Packed_HoldsFireAndKeepsItsTurretStowed_UntilDeployed()
        {
            var launcher = Launcher(turreted: true);
            var enemy = _world.SpawnUnit(2, new float3(-4f, 0f, 0f));

            _world.Run(2f);
            Assert.AreEqual(100f, _world.GetHealth(enemy), "packed, it holds fire");
            Assert.AreEqual(0f, Yaw(launcher), 1e-4f, "and keeps its turret stowed");

            Command(CommandType.Deploy, launcher);
            _world.Run(DeployTime + 0.5f);
            Assert.AreEqual(-0.5f * math.PI, Yaw(launcher), 0.05f, "deployed, it turns onto the target");
        }

        [Test]
        public void Deploy_TakesTheDeployTime_ThenFires()
        {
            var launcher = Launcher();
            var enemy = _world.SpawnUnit(2, new float3(0f, 0f, 4f));

            Command(CommandType.Deploy, launcher);
            _world.Run(DeployTime - 0.2f);
            Assert.AreEqual(DeployState.Deploying, State(launcher));
            Assert.AreEqual(100f, _world.GetHealth(enemy));

            _world.Run(0.3f);
            Assert.AreEqual(DeployState.Deployed, State(launcher));
            Assert.AreEqual(1f, _world.Get<Deployable>(launcher).Progress);
            Assert.IsTrue(_world.RunUntil(() => _world.GetHealth(enemy) < 100f, 1f));
        }

        [Test]
        public void Deployed_RefusesMoves()
        {
            var launcher = Deployed();

            Command(CommandType.Move, launcher, new float3(10f, 0f, 0f));
            _world.Run(1f);

            Assert.IsFalse(_world.IsEnabled<ActiveOrder>(launcher));
            Assert.AreEqual(0f, _world.Get<LocalTransform>(launcher).Position.x, 1e-3f);
        }

        [Test]
        public void PackUp_TakesThePackUpTime_ThenMoves()
        {
            var launcher = Deployed();

            Command(CommandType.PackUp, launcher);
            _world.Run(PackUpTime - 0.2f);
            Assert.AreEqual(DeployState.Packing, State(launcher));
            Command(CommandType.Move, launcher, new float3(10f, 0f, 0f));
            _world.Run(0.3f);
            Assert.AreEqual(DeployState.Packed, State(launcher));
            Assert.AreEqual(0f, _world.Get<LocalTransform>(launcher).Position.x, 1e-3f, "still packing when ordered");

            Command(CommandType.Move, launcher, new float3(10f, 0f, 0f));
            _world.Run(1f);
            Assert.Greater(_world.Get<LocalTransform>(launcher).Position.x, 1f);
        }

        [Test]
        public void Deploy_HaltsAMovingUnit()
        {
            var launcher = Launcher();
            Command(CommandType.Move, launcher, new float3(20f, 0f, 0f));
            _world.Run(0.5f);

            Command(CommandType.Deploy, launcher);
            _world.Run(0.5f);
            var stopped = _world.Get<LocalTransform>(launcher).Position.x;
            _world.Run(1f);

            Assert.IsFalse(_world.IsEnabled<ActiveOrder>(launcher));
            Assert.AreEqual(stopped, _world.Get<LocalTransform>(launcher).Position.x, 1e-3f);
        }

        private Entity Launcher(bool turreted = false)
        {
            var launcher = _world.AddWeapon(_world.SpawnUnit(1, float3.zero), range: 6f, damage: 10f, cooldown: 0.5f,
                turreted: turreted);
            _world.AddDeployable(launcher, DeployTime, PackUpTime);
            return launcher;
        }

        private Entity Deployed()
        {
            var launcher = Launcher();
            Command(CommandType.Deploy, launcher);
            _world.Run(DeployTime + 0.2f);
            return launcher;
        }

        private void Command(CommandType type, Entity unit, float3 position = default) =>
            _world.Command(1, new PlayerCommand { Type = type, Unit = unit, Position = position });

        private DeployState State(Entity unit) => _world.Get<Deployable>(unit).State;

        private float Yaw(Entity unit) => _world.EntityManager.GetBuffer<WeaponPose>(unit)[0].YawAngle;
    }
}
