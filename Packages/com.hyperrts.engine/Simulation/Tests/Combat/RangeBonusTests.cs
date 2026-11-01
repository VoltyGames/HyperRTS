using HyperRTS.Simulation.Combat.Projectiles;
using HyperRTS.Simulation.Combat.Weapons;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Common.Authoring;
using HyperRTS.Simulation.Tests.Transport;
using HyperRTS.Simulation.Transport;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Tests.Combat
{
    /// <summary>Direct fire reaches further from above its target, within a cap, and from a garrison that grants it.</summary>
    public class RangeBonusTests : SimulationFixture
    {
        private const float Range = 10f;

        [SetUp]
        public void Prepare()
        {
            World.DisableFog();
        }

        // +1 % a metre above the target, at most +10 %; shells lobbed from afar gain nothing.
        [TestCase(0f, Trajectory.Direct, 1f)]
        [TestCase(5f, Trajectory.Direct, 1.05f)]
        [TestCase(30f, Trajectory.Direct, 1.1f)]
        [TestCase(30f, Trajectory.Ballistic, 1f)]
        public void DirectFire_ReachesFurther_FromAbove(float height, Trajectory trajectory, float scale)
        {
            // The shooter stands at the cliff's edge, so its line down to the target clears the cliff top.
            World.CreateTerrain(p => p.x < 3f ? height : 0f);
            var shooter = World.SpawnUnit(1, new float3(1f, height, 0f));
            World.AddWeapon(shooter, range: Range, trajectory: trajectory, arcHeight: 5f);
            World.SpawnUnit(2, new float3(8f, 0f, 0f));

            Assert.IsTrue(World.RunUntil(() => World.IsEnabled<AttackTarget>(shooter), 2f), "never engaged");

            Assert.AreEqual(Range * scale, World.Get<AttackTarget>(shooter).Reach, 1e-3f);
        }

        [TestCase(true, 1.1f, TestName = "GarrisonedSquad_OutrangesItselfOutside")]
        [TestCase(false, 1f, TestName = "SameSquadOutside_KeepsItsRange")]
        public void Garrison_LengthensItsPassengersRange(bool garrisoned, float scale)
        {
            var house = World.SpawnBuilding(1, float3.zero, new float2(4f, 4f));
            var writer = World.Writer(house);
            TransportSetup.AddContainer(writer, new Container { Capacity = 4, MaxPassengerSize = 1, PassengersFire = true });
            writer.Add(new PassengerRange { Bonus = 0.1f });
            var squad = World.SpawnUnit(1, new float3(0f, 0f, -5f));
            var passenger = World.Writer(squad);
            TransportSetup.AddPassenger(passenger, 1);
            World.AddWeapon(squad, range: Range);
            if (garrisoned)
            {
                World.Enter(squad, house);
                Assert.IsTrue(World.RunUntil(() => World.IsInside(squad), 5f), "never got in");
            }

            World.SpawnUnit(2, new float3(9f, 0f, 0f));

            Assert.IsTrue(World.RunUntil(() => World.IsEnabled<AttackTarget>(squad), 2f), "never engaged");
            Assert.AreEqual(Range * scale, World.Get<AttackTarget>(squad).Reach, 1e-3f);
        }
    }
}
