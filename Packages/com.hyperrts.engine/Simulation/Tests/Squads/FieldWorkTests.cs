using HyperRTS.Simulation.Buildings;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Common.Authoring;
using HyperRTS.Simulation.Common.Infantry;
using HyperRTS.Simulation.Orders;
using HyperRTS.Simulation.Squads;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Tests.Squads
{
    /// <summary>
    /// Squads building a field work: progress for each living member, members spread along the site and dig, and the
    /// site's cover spots count only once it is built.
    /// </summary>
    public class FieldWorkTests : SimulationFixture
    {
        private const float BuildTime = 100f;
        private static readonly float2 Footprint = new(3f, 1f);

        private Entity Squad(float3 position, int size)
        {
            var squad = World.SpawnSquad(1, position, new byte[size], speed: 4f);
            World.AddWeapon(squad, range: 6f, damage: 10f, cooldown: 100f);
            return World.MakeBuilder(squad);
        }

        private Entity Site(float3 position) =>
            World.SpawnBuilding(1, position, Footprint, complete: false, buildTime: BuildTime);

        private Entity Dig(Entity squad, Entity site)
        {
            World.Order(squad, OrderType.Build, site);
            return site;
        }

        [Test]
        public void Squad_BuildsAtOneRatePerLivingMember()
        {
            var four = Dig(Squad(float3.zero, 4), Site(new float3(10f, 0f, 0f)));
            var two = Dig(Squad(new float3(0f, 0f, 30f), 2), Site(new float3(10f, 0f, 30f)));

            World.Run(6f);

            var fast = World.Get<ConstructionProgress>(four).Value;
            var slow = World.Get<ConstructionProgress>(two).Value;
            Assert.Greater(slow, 0f);
            Assert.AreEqual(2f, fast / slow, 0.1f);
        }

        [Test]
        public void Members_SpreadAlongOneSideOfTheSite_AndDig()
        {
            var squad = Squad(float3.zero, 4);
            var site = Dig(squad, Site(new float3(10f, 0f, 0f)));

            World.Run(6f);

            var place = World.GetPosition(squad);
            var xs = new System.Collections.Generic.List<float>();
            foreach (var member in World.Buffer<SquadMember>(squad))
            {
                var at = member.Position(place);
                xs.Add(at.x);
                Assert.AreEqual(MemberAction.Dig, member.Action);
                Assert.AreEqual(Posture.Stand, member.Posture);
                // The far members stand clamped to the squad's reach, so only the side is exact.
                Assert.That(at.z - World.GetPosition(site).z, Is.InRange(0.8f, SquadWork.Standoff + Footprint.y * 0.5f + 0.05f));
            }

            Assert.Greater(math.max(math.max(xs[0], xs[1]), math.max(xs[2], xs[3])) - math.min(math.min(xs[0], xs[1]), math.min(xs[2], xs[3])), 1.5f);
        }

        [Test]
        public void Members_StopDigging_WhenTheSiteIsBuilt()
        {
            var squad = Squad(float3.zero, 2);
            var site = Dig(squad, Site(new float3(8f, 0f, 0f)));
            World.Run(4f);

            World.Set(site, new ConstructionProgress { Value = 1f });
            World.Run(2f);

            foreach (var member in World.Buffer<SquadMember>(squad))
            {
                Assert.AreEqual(MemberAction.None, member.Action);
            }
        }

        [Test]
        public void CoverSpots_OfASite_AreTakenOnlyOnceItIsBuilt_AndATrenchSpotStandsTheSoldier()
        {
            var site = World.SpawnBuilding(0, new float3(0f, 0f, 2f), Footprint, complete: false);
            World.EntityManager.AddBuffer<CoverSpot>(site).Add(new CoverSpot
            {
                Position = new float3(0f, -1.2f, -2f), Facing = new float2(0f, 1f), Kind = CoverKind.Heavy,
            });
            var squad = World.SpawnUnit(1, float3.zero);
            SquadAuthoring.Add(World.Writer(squad), 25f, new byte[1]);
            World.AddWeapon(squad, range: 6f, damage: 10f, cooldown: 100f);

            World.Run(2f);
            Assert.IsFalse(World.Buffer<SquadMember>(squad)[0].HasSpot, "nothing to stand behind yet");

            World.Set(site, new ConstructionProgress { Value = 1f });
            World.Run(3f);
            var member = World.Buffer<SquadMember>(squad)[0];
            Assert.AreEqual(CoverKind.Heavy, member.Cover);
            Assert.AreEqual(1.2f, member.Sink, 1e-3f, "the spot lies 1.2 m below the ground");
            Assert.AreEqual(Posture.Stand, member.Posture, "walls to the shoulders, so the soldier stands");
        }
    }
}
