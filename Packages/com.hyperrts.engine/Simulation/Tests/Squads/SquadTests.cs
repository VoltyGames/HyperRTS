using HyperRTS.Simulation.Combat.Damage;
using HyperRTS.Simulation.Combat.Weapons;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Common.Authoring;
using HyperRTS.Simulation.Orders;
using HyperRTS.Simulation.Production;
using HyperRTS.Simulation.Squads;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Tests.Squads
{
    /// <summary>
    /// A squad of four, 25 health each: hits land on members without carrying over, splash and heals go by member,
    /// weapons follow their living carriers, and members walk to their slots. Health always equals the members' sum.
    /// </summary>
    public class SquadTests : SimulationFixture
    {
        private const float Each = 25f;
        private const int Research = 4242;

        private Entity Squad(float3 position, params byte[] weapons)
        {
            var squad = World.SpawnUnit(1, position, maxHealth: Each * weapons.Length);
            SquadAuthoring.Add(World.Writer(squad), Each * weapons.Length, weapons);
            return squad;
        }

        private Entity ArmedSquad(float3 position, params byte[] weapons)
        {
            var squad = Squad(position, weapons);
            World.AddWeapon(squad, range: 6f, damage: 10f, cooldown: 100f);
            return squad;
        }

        private void Hit(Entity target, float amount, float3 at = default, float radius = 0f, float edge = 1f) =>
            World.QueueDamage(new DamageEvent
            {
                Target = target,
                Position = at,
                Origin = at,
                SourceFaction = 2,
                Amount = amount,
                Radius = radius,
                EdgeFactor = edge,
            });

        private float Member(Entity squad, int index) => World.Buffer<SquadMember>(squad)[index].Health;

        private int Hurt(Entity squad)
        {
            var hurt = 0;
            foreach (var member in World.Buffer<SquadMember>(squad))
            {
                hurt += member.Health < member.MaxHealth ? 1 : 0;
            }

            return hurt;
        }

        private void AssertHealthIsMemberSum(Entity squad)
        {
            var members = World.Buffer<SquadMember>(squad);
            Assert.AreEqual(SquadMember.Total(members), World.GetHealth(squad), 1e-3f);
            Assert.AreEqual(SquadMember.TotalMax(members), World.Get<Health>(squad).Max, 1e-3f);
        }

        [Test]
        public void DirectHit_LandsOnOneMemberAndDoesNotCarryOver()
        {
            var squad = Squad(float3.zero, 0, 0, 0, 0);

            Hit(squad, 40f);
            World.Tick();

            Assert.AreEqual(1, Hurt(squad), "one member took the hit");
            Assert.AreEqual(3f * Each, World.GetHealth(squad), 1e-3f, "the 15 past its health is lost");
            AssertHealthIsMemberSum(squad);
        }

        [Test]
        public void DirectHits_SpreadOverMembers()
        {
            var squad = Squad(float3.zero, 0, 0, 0, 0);

            for (var i = 0; i < 12; i++)
            {
                Hit(squad, 1f);
            }

            World.Tick();

            Assert.GreaterOrEqual(Hurt(squad), 2);
            Assert.AreEqual(Each * 4f - 12f, World.GetHealth(squad), 1e-3f);
            AssertHealthIsMemberSum(squad);
        }

        [Test]
        public void Splash_HurtsEachMemberByItsOwnDistanceEvenPastTheSquadsRadius()
        {
            var squad = Squad(float3.zero, 0, 0, 0, 0);
            var members = World.Buffer<SquadMember>(squad);
            float[] stand = { -2f, 0f, 2f, 3f };
            for (var i = 0; i < 4; i++)
            {
                var member = members[i];
                member.Offset = new float2(stand[i], 0f);
                members[i] = member;
            }

            Hit(Entity.Null, 20f, new float3(3f, 0f, 0f), radius: 3f, edge: 0f);
            World.Tick();

            Assert.AreEqual(Each, Member(squad, 0), 0.5f, "outside the blast");
            Assert.Greater(Each, Member(squad, 1) + 0.5f);
            Assert.Greater(Member(squad, 1), Member(squad, 2) + 0.5f, "nearer members lose more");
            Assert.Greater(Member(squad, 2), Member(squad, 3) + 0.5f);
            AssertHealthIsMemberSum(squad);
        }

        [Test]
        public void BlastAimedAtTheSquad_StrikesTheMemberNearestIt()
        {
            var squad = Squad(float3.zero, 0, 0, 0, 0);
            var members = World.Buffer<SquadMember>(squad);
            float[] stand = { -3f, -1f, 1f, 3f };
            for (var i = 0; i < 4; i++)
            {
                var member = members[i];
                member.Offset = new float2(stand[i], 0f);
                members[i] = member;
            }

            Hit(squad, 30f, new float3(3f, 0f, 0f), radius: 0.5f);
            World.Tick();

            Assert.AreEqual(0f, Member(squad, 3), 1e-3f, "the member under the blast");
            Assert.AreEqual(1, Hurt(squad), "the others stand outside the blast");
            AssertHealthIsMemberSum(squad);
        }

        [Test]
        public void Fill_SharesASquadsHealthFromTheFirstMember()
        {
            var members = World.Buffer<SquadMember>(Squad(float3.zero, 0, 0, 0, 0));

            SquadMember.Fill(members, 60f);

            Assert.AreEqual(Each, members[0].Health, 1e-3f);
            Assert.AreEqual(Each, members[1].Health, 1e-3f);
            Assert.AreEqual(10f, members[2].Health, 1e-3f);
            Assert.AreEqual(0f, members[3].Health, 1e-3f, "the last falls first");
        }

        [Test]
        public void Heal_GoesToTheMostHurtMember()
        {
            var squad = Squad(float3.zero, 0, 0, 0, 0);
            var members = World.Buffer<SquadMember>(squad);
            float[] health = { 25f, 10f, 20f, 25f };
            for (var i = 0; i < 4; i++)
            {
                var member = members[i];
                member.Health = health[i];
                members[i] = member;
            }

            World.Set(squad, new Health { Current = 80f, Max = 100f });

            World.QueueDamage(DamageEvent.Direct(squad, float3.zero, Entity.Null, 1, -5f));
            World.Tick();

            Assert.AreEqual(15f, Member(squad, 1), 1e-3f);
            Assert.AreEqual(20f, Member(squad, 2), 1e-3f);
            AssertHealthIsMemberSum(squad);
        }

        [Test]
        public void UpgradedMember_CarriesTheUpgradeWhileItsMountIsEnabled_AndItsOwnWeaponOtherwise()
        {
            var squad = World.SpawnUnit(1, float3.zero, maxHealth: Each * 2);
            SquadAuthoring.Add(World.Writer(squad), Each * 2, new byte[] { 0, 0 }, new byte?[] { null, 1 });
            World.AddWeapon(squad, range: 6f, damage: 10f, cooldown: 100f);
            World.AddMount(squad, range: 6f, damage: 30f, cooldown: 100f, requiredResearchTypeId: Research);
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
        public void EachCarrierFiresItsOwnShot_AndAMountWithoutCarriersStaysSilent()
        {
            var gun = ArmedSquad(float3.zero, 0, 0, 0, 1);
            World.AddMount(gun, range: 6f, damage: 5f, cooldown: 100f);
            var enemy = World.SpawnUnit(2, new float3(4f, 0f, 0f), maxHealth: 1000f);

            World.Run(1f);

            Assert.AreEqual(3, World.Buffer<WeaponMount>(gun)[0].Carriers);
            Assert.AreEqual(1000f - 3f * 10f - 5f, World.GetHealth(enemy), 1e-3f);

            var silenced = ArmedSquad(new float3(0f, 0f, 20f), 0, 0, 0, 1);
            World.AddMount(silenced, range: 6f, damage: 5f, cooldown: 100f);
            var other = World.SpawnUnit(2, new float3(4f, 0f, 20f), maxHealth: 1000f);
            var members = World.Buffer<SquadMember>(silenced);
            var carrier = members[3];
            carrier.Health = 0f;
            members[3] = carrier;

            World.Run(1f);

            Assert.AreEqual(0, World.Mount(silenced).Carriers);
            Assert.AreEqual(1000f - 3f * 10f, World.GetHealth(other), 1e-3f, "only the rifles fired");
            AssertHealthIsMemberSum(silenced);
        }

        [Test]
        public void StatChangeToMaxHealth_ScalesMembersAndKeepsTheSum()
        {
            var squad = ArmedSquad(float3.zero, 0, 0, 0, 0);
            Hit(squad, 10f);
            World.Tick();

            var health = World.Get<Health>(squad);
            World.Set(squad, new Health { Current = health.Current * 2f, Max = health.Max * 2f });
            World.Tick();

            AssertHealthIsMemberSum(squad);
            Assert.AreEqual(180f, World.GetHealth(squad), 1e-2f);
        }

        [Test]
        public void HaltedMembers_WalkToARingAroundTheSquad()
        {
            var squad = ArmedSquad(float3.zero, 0, 0, 0, 0);
            var members = World.Buffer<SquadMember>(squad);
            for (var i = 0; i < 4; i++)
            {
                var member = members[i];
                member.Offset = float2.zero;
                members[i] = member;
            }

            World.Tick();
            Assert.Less(math.length(World.Buffer<SquadMember>(squad)[0].Offset), 0.5f, "a member walks, not jumps");

            World.Run(2f);

            for (var i = 0; i < 4; i++)
            {
                var offset = World.Buffer<SquadMember>(squad)[i].Offset;
                Assert.AreEqual(0f, math.distance(offset, SquadMember.SpreadSlot(i, 4)), 0.05f);
                Assert.LessOrEqual(math.length(offset), SquadMember.MaxSpread);
            }
        }

        [Test]
        public void MovingSquad_FormsAWedgeAlongItsHeading()
        {
            var squad = ArmedSquad(float3.zero, 0, 0, 0, 0);
            World.Issue(squad, new Order { Type = OrderType.Move, Position = new float3(60f, 0f, 0f) });

            World.Run(3f);

            var members = World.Buffer<SquadMember>(squad);
            Assert.Greater(members[0].Offset.x, members[3].Offset.x + 1f, "the point man leads the rear");
            Assert.AreEqual(0f, math.distance(members[1].Offset, members[1].Slot), 0.1f);
        }
    }
}
