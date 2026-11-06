using HyperRTS.Simulation.Combat.Damage;
using HyperRTS.Simulation.Combat.Weapons;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Common.Authoring;
using HyperRTS.Simulation.Common.Infantry;
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
        private Entity Squad(float3 position, params byte[] weapons) => World.SpawnSquad(1, position, weapons);

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

        [Test]
        public void DirectHit_LandsOnOneMemberAndDoesNotCarryOver()
        {
            var squad = Squad(float3.zero, 0, 0, 0, 0);

            World.HitSquad(squad, 40f);
            World.Tick();

            Assert.AreEqual(1, Hurt(squad), "one member took the hit");
            Assert.AreEqual(3f * SquadTestKit.MemberHealth, World.GetHealth(squad), 1e-3f, "the 15 past its health is lost");
            var shot = 0;
            foreach (var member in World.Buffer<SquadMember>(squad))
            {
                shot += member.Death == DeathCause.Shot ? 1 : 0;
            }

            Assert.AreEqual(1, shot, "the fallen member remembers it was shot");
            World.AssertHealthIsMemberSum(squad);
        }

        [Test]
        public void DirectHits_SpreadOverMembers()
        {
            var squad = Squad(float3.zero, 0, 0, 0, 0);

            for (var i = 0; i < 12; i++)
            {
                World.HitSquad(squad, 1f);
            }

            World.Tick();

            Assert.GreaterOrEqual(Hurt(squad), 2);
            Assert.AreEqual(SquadTestKit.MemberHealth * 4f - 12f, World.GetHealth(squad), 1e-3f);
            World.AssertHealthIsMemberSum(squad);
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

            World.HitSquad(Entity.Null, 20f, new float3(3f, 0f, 0f), radius: 3f, edge: 0f);
            World.Tick();

            Assert.AreEqual(SquadTestKit.MemberHealth, Member(squad, 0), 0.5f, "outside the blast");
            Assert.Greater(SquadTestKit.MemberHealth, Member(squad, 1) + 0.5f);
            Assert.Greater(Member(squad, 1), Member(squad, 2) + 0.5f, "nearer members lose more");
            Assert.Greater(Member(squad, 2), Member(squad, 3) + 0.5f);
            World.AssertHealthIsMemberSum(squad);
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

            World.HitSquad(squad, 30f, new float3(3f, 0f, 0.4f), radius: 0.5f);
            World.Tick();

            Assert.AreEqual(0f, Member(squad, 3), 1e-3f, "the member under the blast");
            var dead = World.Buffer<SquadMember>(squad)[3];
            Assert.AreEqual(DeathCause.Blast, dead.Death);
            Assert.Greater(dead.BlastDirection.y, 0.9f, "it fell away from the blast beside it");
            Assert.AreEqual(1, Hurt(squad), "the others stand outside the blast");
            World.AssertHealthIsMemberSum(squad);
        }

        [Test]
        public void Fill_KeepsTheDeadDeadAndSharesTheHealthAmongTheLiving()
        {
            var members = World.Buffer<SquadMember>(Squad(float3.zero, 0, 0, 0, 0));

            SquadMember.Fill(members, 40f, 0b1010);

            Assert.AreEqual(0f, members[0].Health, 1e-3f);
            Assert.AreEqual(20f, members[1].Health, 1e-3f);
            Assert.AreEqual(0f, members[2].Health, 1e-3f);
            Assert.AreEqual(20f, members[3].Health, 1e-3f);
            Assert.AreEqual(0b1010, SquadMember.LivingMask(members));
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
            World.AssertHealthIsMemberSum(squad);
        }

        [Test]
        public void StatChangeToMaxHealth_ScalesMembersAndKeepsTheSum()
        {
            var squad = World.SpawnArmedSquad(float3.zero, 0, 0, 0, 0);
            World.HitSquad(squad, 10f);
            World.Tick();

            var health = World.Get<Health>(squad);
            World.Set(squad, new Health { Current = health.Current * 2f, Max = health.Max * 2f });
            World.Tick();

            World.AssertHealthIsMemberSum(squad);
            Assert.AreEqual(180f, World.GetHealth(squad), 1e-2f);
        }

        [Test]
        public void HaltedMembers_WalkToARingAroundTheSquad()
        {
            var squad = World.SpawnArmedSquad(float3.zero, 0, 0, 0, 0);
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

            var rules = World.Singleton<SquadRules>();
            for (var i = 0; i < 4; i++)
            {
                var offset = World.Buffer<SquadMember>(squad)[i].Offset;
                Assert.AreEqual(0f, math.distance(offset, rules.SpreadSlot(i, 4)), 0.05f);
                Assert.LessOrEqual(math.length(offset), rules.MaxSpread);
            }
        }

        [Test]
        public void Ring_GrowsWithTheLivingCount_UpToTheMaxSpread()
        {
            var rules = SquadSettings.Standard.Rules;

            Assert.Less(rules.RingRadius(2), rules.RingRadius(4));
            Assert.Less(rules.RingRadius(4), rules.RingRadius(6));
            Assert.GreaterOrEqual(rules.RingRadius(4), 2.2f);
            Assert.AreEqual(rules.MaxSpread, rules.RingRadius(20), 1e-4f);
            Assert.AreEqual(rules.RingRadius(6), math.length(rules.SpreadSlot(3, 6)), 1e-4f);
        }

        [Test]
        public void Splash_ReachesADiggingMemberFiveMetresOut()
        {
            var squad = Squad(float3.zero, 0, 0);
            var members = World.Buffer<SquadMember>(squad);
            var member = members[1];
            member.Offset = new float2(5f, 0f);
            members[1] = member;

            World.HitSquad(Entity.Null, 20f, new float3(5f, 0f, 0f), radius: 1f, edge: 0f);
            World.Tick();

            Assert.AreEqual(SquadTestKit.MemberHealth, Member(squad, 0), 0.5f, "out of the blast");
            Assert.Greater(SquadTestKit.MemberHealth, Member(squad, 1) + 0.5f);
        }

        [Test]
        public void MovingSquad_FormsAWedgeAlongItsHeading()
        {
            var squad = World.SpawnArmedSquad(float3.zero, 0, 0, 0, 0);
            World.Issue(squad, new Order { Type = OrderType.Move, Position = new float3(60f, 0f, 0f) });

            World.Run(3f);

            var members = World.Buffer<SquadMember>(squad);
            Assert.Greater(members[0].Offset.x, members[3].Offset.x + 1f, "the point man leads the rear");
            Assert.AreEqual(0f, math.distance(members[1].Offset, members[1].Slot), 0.1f);
        }
    }
}
