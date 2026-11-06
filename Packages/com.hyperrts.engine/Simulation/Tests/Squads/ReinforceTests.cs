using HyperRTS.Simulation.Combat.Weapons;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Common.Infantry;
using HyperRTS.Simulation.Orders;
using HyperRTS.Simulation.Resources;
using HyperRTS.Simulation.Squads;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Tests.Squads
{
    /// <summary>
    /// Reinforcing brings dead members back one at a time near a friendly point, at a share of the squad's price each,
    /// and keeps the squad's health and weapon carriers in step. It is refused when poor or out of range.
    /// </summary>
    public class ReinforceTests : SimulationFixture
    {
        private const int Funds = 500;

        private ResourceType _supplies;
        private Entity _point;

        private Entity Squad(int funds, float pointRange = 25f)
        {
            _supplies ??= World.CreateSupplies();
            World.AddResources(1, _supplies, funds);
            var squad = World.SpawnSquad(1, float3.zero);
            World.AddWeapon(squad, range: 6f, damage: 10f, cooldown: 100f);
            World.EntityManager.AddBuffer<ResourceCost>(squad);
            World.SetCost(squad, _supplies, 100);
            var members = World.Buffer<SquadMember>(squad);
            for (var i = 2; i < 4; i++)
            {
                var member = members[i];
                member.Health = 0f;
                members[i] = member;
            }

            _point = World.SpawnBuilding(1, new float3(10f, 0f, 0f), new float2(4f, 4f));
            World.Add(_point, new ReinforcePoint { Range = pointRange });
            return squad;
        }

        private void Reinforce(Entity squad) =>
            World.Command(1, new PlayerCommand { Type = CommandType.Reinforce, Unit = squad });

        private int Living(Entity squad) => SquadMember.CountLiving(World.Buffer<SquadMember>(squad));

        private int Spent(int funds = Funds) => funds - World.GetResources(1, _supplies);

        [Test]
        public void Reinforce_BringsMembersBackOneAtATimeForAShareOfThePrice()
        {
            var squad = Squad(Funds);

            Reinforce(squad);
            World.Run(3f);
            Assert.AreEqual(2, Living(squad));
            Assert.AreEqual(18, Spent(), "75 percent of a quarter of 100, paid when the member's turn starts");

            World.Run(1.5f);
            Assert.AreEqual(3, Living(squad));
            Assert.AreEqual(36, Spent());
            Assert.AreEqual(SquadTestKit.MemberHealth * 3, World.Get<Health>(squad).Current, 1e-3f, "the squad's health is its members' sum");
            Assert.AreEqual(3, World.Buffer<WeaponMount>(squad)[0].Carriers, "the revived member carries a weapon");

            World.Run(5f);
            Assert.AreEqual(4, Living(squad));
            Assert.AreEqual(36, Spent(), "two members, two payments");
            Assert.IsFalse(World.IsEnabled<Reinforcing>(squad));
            Assert.AreEqual(SquadTestKit.MemberHealth * 4, World.Get<Health>(squad).Current, 1e-3f);
        }

        [Test]
        public void Reinforce_IsRefusedWhenPoor()
        {
            var squad = Squad(funds: 10);

            Reinforce(squad);
            World.Run(6f);

            Assert.AreEqual(2, Living(squad), "10 supplies can't pay 18");
            Assert.AreEqual(0, Spent(10));
            Assert.IsFalse(World.IsEnabled<Reinforcing>(squad));
        }

        [Test]
        public void Reinforce_IsRefusedOutOfRange()
        {
            var squad = Squad(Funds, pointRange: 5f);

            Reinforce(squad);
            World.Run(6f);

            Assert.AreEqual(2, Living(squad), "the point is 10 m away and reaches 5");
        }

        [Test]
        public void Reinforce_LeavingRangeCancelsAndRefundsTheMemberInProgress()
        {
            var squad = Squad(Funds);
            Reinforce(squad);
            World.Run(0.5f);
            Assert.AreEqual(18, Spent());

            World.Command(1, new PlayerCommand { Type = CommandType.Move, Unit = squad, Position = new float3(-80f, 0f, 0f) });
            World.Run(8f);

            Assert.AreEqual(2, Living(squad));
            Assert.AreEqual(0, Spent(), "refunded");
            Assert.IsFalse(World.IsEnabled<Reinforcing>(squad));
        }

        [TestCase(Funds, true, SquadRefusal.None)]
        [TestCase(Funds, false, SquadRefusal.NoPoint)]
        [TestCase(1, true, SquadRefusal.TooPoor)]
        public void Rule_NamesWhatIsInTheWay(int funds, bool inRange, SquadRefusal expected)
        {
            var squad = Squad(funds);
            var rules = SquadSettings.Standard.Rules;
            var cost = World.EntityManager.GetBuffer<ResourceCost>(squad);
            var stock = World.EntityManager.GetBuffer<ResourceStock>(World.Player(1));
            var members = World.Buffer<SquadMember>(squad);

            Assert.AreEqual(expected, SquadOrderRule.FindReinforceRefusal(members, inRange, rules, cost, stock));
            Assert.AreEqual(SquadRefusal.None, SquadOrderRule.FindReinforceRefusal(members, true, rules, default, stock),
                "a squad without a price is free");
            SquadMember.Revive(members);
            SquadMember.Revive(members);
            Assert.AreEqual(SquadRefusal.FullStrength,
                SquadOrderRule.FindReinforceRefusal(members, true, rules, cost, stock));
        }

        [Test]
        public void Reinforce_IsRefusedAtAnOfflinePoint()
        {
            var squad = Squad(Funds);
            World.SetOffline(_point, true);

            Reinforce(squad);
            World.Run(6f);

            Assert.AreEqual(2, Living(squad));
        }
    }
}
