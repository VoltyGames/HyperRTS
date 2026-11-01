using HyperRTS.Simulation.Combat.Weapons;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Orders;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.Tests.Combat
{
    /// <summary>
    /// Deployable units: deploy and pack-up timing, fire and turret held while packed, moves refused while rooted.
    /// </summary>
    public class DeployTests : SimulationFixture
    {
        private const float DeployTime = 1f;
        private const float PackUpTime = 0.6f;

        [Test]
        public void Packed_HoldsFireAndKeepsItsTurretStowed_UntilDeployed()
        {
            var launcher = Launcher(turreted: true);
            var enemy = World.SpawnUnit(2, new float3(-4f, 0f, 0f));

            World.Run(2f);
            Assert.AreEqual(100f, World.GetHealth(enemy), "packed, it holds fire");
            Assert.AreEqual(0f, Yaw(launcher), 1e-4f, "and keeps its turret stowed");

            Command(CommandType.Deploy, launcher);
            World.Run(DeployTime + 0.5f);
            Assert.AreEqual(-0.5f * math.PI, Yaw(launcher), 0.05f, "deployed, it turns onto the target");
        }

        [Test]
        public void Deploy_TakesTheDeployTime_ThenFires()
        {
            var launcher = Launcher();
            var enemy = World.SpawnUnit(2, new float3(0f, 0f, 4f));

            Command(CommandType.Deploy, launcher);
            World.Run(DeployTime - 0.2f);
            Assert.AreEqual(DeployState.Deploying, State(launcher));
            Assert.AreEqual(100f, World.GetHealth(enemy));

            World.Run(0.3f);
            Assert.AreEqual(DeployState.Deployed, State(launcher));
            Assert.AreEqual(1f, World.Get<Deployable>(launcher).Progress);
            Assert.IsTrue(World.RunUntil(() => World.GetHealth(enemy) < 100f, 1f));
        }

        [Test]
        public void Deployed_RefusesMoves()
        {
            var launcher = Deployed();

            Command(CommandType.Move, launcher, new float3(10f, 0f, 0f));
            World.Run(1f);

            Assert.IsFalse(World.IsEnabled<ActiveOrder>(launcher));
            Assert.AreEqual(0f, World.Get<LocalTransform>(launcher).Position.x, 1e-3f);
        }

        [Test]
        public void PackUp_TakesThePackUpTime_ThenMoves()
        {
            var launcher = Deployed();

            Command(CommandType.PackUp, launcher);
            World.Run(PackUpTime - 0.2f);
            Assert.AreEqual(DeployState.Packing, State(launcher));
            Command(CommandType.Move, launcher, new float3(10f, 0f, 0f));
            World.Run(0.3f);
            Assert.AreEqual(DeployState.Packed, State(launcher));
            Assert.AreEqual(0f, World.Get<LocalTransform>(launcher).Position.x, 1e-3f, "still packing when ordered");

            Command(CommandType.Move, launcher, new float3(10f, 0f, 0f));
            World.Run(1f);
            Assert.Greater(World.Get<LocalTransform>(launcher).Position.x, 1f);
        }

        [Test]
        public void Deploy_HaltsAMovingUnit()
        {
            var launcher = Launcher();
            Command(CommandType.Move, launcher, new float3(20f, 0f, 0f));
            World.Run(0.5f);

            Command(CommandType.Deploy, launcher);
            World.Run(0.5f);
            var stopped = World.Get<LocalTransform>(launcher).Position.x;
            World.Run(1f);

            Assert.IsFalse(World.IsEnabled<ActiveOrder>(launcher));
            Assert.AreEqual(stopped, World.Get<LocalTransform>(launcher).Position.x, 1e-3f);
        }

        private Entity Launcher(bool turreted = false)
        {
            var launcher = World.AddWeapon(World.SpawnUnit(1, float3.zero), range: 6f, damage: 10f, cooldown: 0.5f,
                turreted: turreted);
            World.AddDeployable(launcher, DeployTime, PackUpTime);
            return launcher;
        }

        private Entity Deployed()
        {
            var launcher = Launcher();
            Command(CommandType.Deploy, launcher);
            World.Run(DeployTime + 0.2f);
            return launcher;
        }

        private void Command(CommandType type, Entity unit, float3 position = default) =>
            World.Command(1, new PlayerCommand { Type = type, Unit = unit, Position = position });

        private DeployState State(Entity unit) => World.Get<Deployable>(unit).State;

        private float Yaw(Entity unit) => World.Buffer<WeaponPose>(unit)[0].YawAngle;
    }
}
