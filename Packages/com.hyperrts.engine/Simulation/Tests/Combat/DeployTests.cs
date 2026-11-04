using HyperRTS.Simulation.Combat.Weapons;
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

        [Test]
        public void FiringArc_KeepsAutoTargetingToTheConeTheUnitDeployedFacing()
        {
            var launcher = Deployed(arcDegrees: 30f);
            var behind = World.SpawnUnit(2, new float3(0f, 0f, -3f));
            var ahead = World.SpawnUnit(2, new float3(0f, 0f, 5f));

            World.Run(2f);

            Assert.AreEqual(100f, World.GetHealth(behind), "outside the cone, though nearer");
            Assert.Less(World.GetHealth(ahead), 100f);
            Assert.AreEqual(DeployState.Deployed, State(launcher), "it never repacks for a target it picked");
        }

        [Test]
        public void AttackOrderOutsideTheArc_PacksUpTurnsAndDeploysAgainBeforeFiring()
        {
            var launcher = Deployed(arcDegrees: 30f);
            var enemy = World.SpawnUnit(2, new float3(4f, 0f, 0f));

            Command(CommandType.Attack, launcher, target: enemy);
            var elapsed = RunUntilHit(enemy);

            Assert.GreaterOrEqual(elapsed, PackUpTime + DeployTime, "it packed up and set up again first");
            Assert.AreEqual(1f, World.Get<Deployable>(launcher).DeployedHeading.x, 0.05f, "now facing the target");
        }

        [Test]
        public void AttackOrderOnAPackedUnit_TurnsToTheTargetAndDeploysFacingIt()
        {
            var launcher = Launcher(arcDegrees: 30f);
            var enemy = World.SpawnUnit(2, new float3(-4f, 0f, 0f));

            Command(CommandType.Attack, launcher, target: enemy);
            var elapsed = RunUntilHit(enemy);

            Assert.GreaterOrEqual(elapsed, DeployTime, "set-up time respected");
            Assert.AreEqual(-1f, World.Get<Deployable>(launcher).DeployedHeading.x, 0.05f);
        }

        private float RunUntilHit(Entity enemy)
        {
            var elapsed = 0f;
            while (World.GetHealth(enemy) >= 100f && elapsed < 10f)
            {
                World.Run(0.1f);
                elapsed += 0.1f;
            }

            Assert.Less(elapsed, 10f, "it never fired");
            return elapsed;
        }

        private Entity Launcher(bool turreted = false, float arcDegrees = 0f)
        {
            var launcher = World.AddWeapon(World.SpawnUnit(1, float3.zero), range: 6f, damage: 10f, cooldown: 0.5f,
                turreted: turreted);
            World.AddDeployable(launcher, DeployTime, PackUpTime);
            var weapon = World.Primary(launcher);
            weapon.FiringArc = math.radians(arcDegrees);
            World.SetPrimary(launcher, weapon);
            return launcher;
        }

        private Entity Deployed(float arcDegrees = 0f)
        {
            var launcher = Launcher(arcDegrees: arcDegrees);
            Command(CommandType.Deploy, launcher);
            World.Run(DeployTime + 0.2f);
            return launcher;
        }

        private void Command(CommandType type, Entity unit, float3 position = default, Entity target = default) =>
            World.Command(1, new PlayerCommand { Type = type, Unit = unit, Position = position, Target = target });

        private DeployState State(Entity unit) => World.Get<Deployable>(unit).State;

        private float Yaw(Entity unit) => World.Buffer<WeaponPose>(unit)[0].YawAngle;
    }
}
