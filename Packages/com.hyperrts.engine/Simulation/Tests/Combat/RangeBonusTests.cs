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
    public class RangeBonusTests
    {
        private const float Range = 10f;

        private TestWorld _world;

        [SetUp]
        public void SetUp()
        {
            _world = new TestWorld();
            _world.CreateMatch(1, 2);
            _world.DisableFog();
        }

        [TearDown]
        public void TearDown() => _world.Dispose();

        // +1 % a metre above the target, at most +10 %; shells lobbed from afar gain nothing.
        [TestCase(0f, Trajectory.Direct, 1f)]
        [TestCase(5f, Trajectory.Direct, 1.05f)]
        [TestCase(30f, Trajectory.Direct, 1.1f)]
        [TestCase(30f, Trajectory.Ballistic, 1f)]
        public void DirectFire_ReachesFurther_FromAbove(float height, Trajectory trajectory, float scale)
        {
            // The shooter stands at the cliff's edge, so its line down to the target clears the cliff top.
            _world.CreateTerrain(p => p.x < 3f ? height : 0f);
            var shooter = _world.SpawnUnit(1, new float3(1f, height, 0f));
            _world.AddWeapon(shooter, range: Range, trajectory: trajectory, arcHeight: 5f);
            _world.SpawnUnit(2, new float3(8f, 0f, 0f));

            Assert.IsTrue(_world.RunUntil(() => _world.IsEnabled<AttackTarget>(shooter), 2f), "never engaged");

            Assert.AreEqual(Range * scale, _world.Get<AttackTarget>(shooter).Reach, 1e-3f);
        }

        [TestCase(true, 1.1f, TestName = "GarrisonedSquad_OutrangesItselfOutside")]
        [TestCase(false, 1f, TestName = "SameSquadOutside_KeepsItsRange")]
        public void Garrison_LengthensItsPassengersRange(bool garrisoned, float scale)
        {
            var house = _world.SpawnBuilding(1, float3.zero, new float2(4f, 4f));
            var writer = new EntityWriter(_world.EntityManager, house);
            TransportSetup.AddContainer(writer, new Container { Capacity = 4, MaxPassengerSize = 1, PassengersFire = true });
            writer.Add(new PassengerRange { Bonus = 0.1f });
            var squad = _world.SpawnUnit(1, new float3(0f, 0f, -5f));
            var passenger = new EntityWriter(_world.EntityManager, squad);
            TransportSetup.AddPassenger(passenger, 1);
            _world.AddWeapon(squad, range: Range);
            if (garrisoned)
            {
                _world.Enter(squad, house);
                Assert.IsTrue(_world.RunUntil(() => _world.IsInside(squad), 5f), "never got in");
            }

            _world.SpawnUnit(2, new float3(9f, 0f, 0f));

            Assert.IsTrue(_world.RunUntil(() => _world.IsEnabled<AttackTarget>(squad), 2f), "never engaged");
            Assert.AreEqual(Range * scale, _world.Get<AttackTarget>(squad).Reach, 1e-3f);
        }
    }
}
