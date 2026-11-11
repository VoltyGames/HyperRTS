using HyperRTS.Simulation.Combat.Deploy;
using HyperRTS.Simulation.Combat.Weapons;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Common.Authoring;
using HyperRTS.Simulation.Common.Infantry;
using HyperRTS.Simulation.Common.Players;
using HyperRTS.Simulation.Production;
using HyperRTS.Simulation.Squads;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Tests.Squads
{
    /// <summary>A squad's weapons follow their living carriers, and an upgrade shows only while its mount is enabled.</summary>
    public class SquadWeaponTests : SquadFixture
    {
        private const int Research = 4242;

        [Test]
        public void UpgradedMember_CarriesTheUpgradeWhileItsMountIsEnabled_AndItsOwnWeaponOtherwise()
        {
            var squad = World.SpawnUnit(1, float3.zero, new TestUnit { MaxHealth = SquadTestKit.MemberHealth * 2 });
            SquadAuthoring.Add(World.Writer(squad), SquadTestKit.MemberHealth * 2, new byte[] { 0, 0 }, new byte?[] { null, 1 });
            World.AddWeapon(squad, new TestWeapon { Range = 6f, Damage = 10f, Cooldown = 100f });
            World.AddMount(squad, new TestMount
            {
                Range = 6f,
                Damage = 30f,
                Cooldown = 100f,
                RequiredResearchTypeId = Research,
            });
            World.Tick();
            Assert.AreEqual(0, World.Buffer<SquadMember>(squad)[1].Weapon, "locked");

            var research = World.EntityManager.GetBuffer<CompletedResearch>(World.Player(1));
            research.Add(new CompletedResearch { TypeId = Research });
            World.Run(0.2f);
            var members = World.Buffer<SquadMember>(squad);
            Assert.AreEqual(1, members[1].Weapon, "researched");
            Assert.AreEqual(0, members[0].Weapon, "a member without an upgrade keeps its weapon");
            Assert.AreEqual(1, World.Buffer<WeaponMount>(squad)[0].Carriers);
            Assert.AreEqual(1, World.Mount(squad).Carriers);

            research.Clear();
            World.Run(0.2f);
            Assert.AreEqual(0, World.Buffer<SquadMember>(squad)[1].Weapon, "locked again");
            Assert.AreEqual(2, World.Buffer<WeaponMount>(squad)[0].Carriers);
        }

        [Test]
        public void CrewMember_StandsAtItsPlaceTurnedByTheHeading_ProneAndUncovered_WhileTheWeaponIsSetUp()
        {
            var squad = World.SpawnArmedSquad(float3.zero, 0, 0);
            var members = World.Buffer<SquadMember>(squad);
            var gunner = members[0];
            gunner.TakesCrewPlace = true;
            gunner.CrewOffset = new float2(0f, -2f);
            gunner.CrewPosture = Posture.Prone;
            members[0] = gunner;
            World.EntityManager.AddComponentData(squad, new Deployable
            {
                State = DeployState.Deployed, DeployTime = 1f, PackUpTime = 1f, Progress = 1f, DeployedHeading = new float2(1f, 0f),
            });

            World.Run(3f);

            gunner = World.Buffer<SquadMember>(squad)[0];
            Assert.AreEqual(-2f, gunner.Offset.x, 0.05f, "two metres behind a gun that points along x");
            Assert.AreEqual(0f, gunner.Offset.y, 0.05f);
            Assert.AreEqual(Posture.Prone, gunner.Posture);
            Assert.AreEqual(CoverKind.None, gunner.Cover);
            Assert.IsFalse(gunner.HasSpot);
        }

        [Test]
        public void EachCarrierFiresItsOwnShot_AndAMountWithoutCarriersStaysSilent()
        {
            var gun = World.SpawnArmedSquad(float3.zero, 0, 0, 0, 1);
            World.AddMount(gun, new TestMount { Range = 6f, Damage = 5f, Cooldown = 100f });
            var enemy = World.SpawnUnit(2, new float3(4f, 0f, 0f), new TestUnit { MaxHealth = 1000f });

            World.Run(1f);

            Assert.AreEqual(3, World.Buffer<WeaponMount>(gun)[0].Carriers);
            Assert.AreEqual(1000f - 3f * 10f - 5f, World.GetHealth(enemy), 1e-3f);

            var silenced = World.SpawnArmedSquad(new float3(0f, 0f, 20f), 0, 0, 0, 1);
            World.AddMount(silenced, new TestMount { Range = 6f, Damage = 5f, Cooldown = 100f });
            var other = World.SpawnUnit(2, new float3(4f, 0f, 20f), new TestUnit { MaxHealth = 1000f });
            var members = World.Buffer<SquadMember>(silenced);
            var carrier = members[3];
            carrier.Health = 0f;
            members[3] = carrier;

            World.Run(1f);

            Assert.AreEqual(0, World.Mount(silenced).Carriers);
            Assert.AreEqual(1000f - 3f * 10f, World.GetHealth(other), 1e-3f, "only the rifles fired");
            World.AssertHealthIsMemberSum(silenced);
        }
    }
}
