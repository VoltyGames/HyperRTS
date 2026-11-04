using HyperRTS.Simulation.Common.Infantry;
using System.Collections.Generic;
using System.Linq;
using HyperRTS.Presentation.HUD;
using HyperRTS.Presentation.HUD.Commands;
using HyperRTS.Presentation.HUD.Panels;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Squads;
using NUnit.Framework;
using Unity.Entities;
using UnityEngine.UIElements;

namespace HyperRTS.Presentation.Tests.HUD
{
    /// <summary>The squad buttons' refusals and the soldier card.</summary>
    public class SquadHUDTests : HUDFixture
    {
        [TestCase(false, false, false, false, SquadCommands.Refusal.FullStrength)]
        [TestCase(true, false, false, false, SquadCommands.Refusal.NoPoint)]
        [TestCase(true, true, false, false, SquadCommands.Refusal.TooPoor)]
        [TestCase(true, true, true, false, SquadCommands.Refusal.None)]
        [TestCase(false, false, false, true, SquadCommands.Refusal.None)]
        public void ReinforceButton_SaysWhyItCannotAct(bool dead, bool inRange, bool affordable, bool working,
            SquadCommands.Refusal expected)
        {
            Assert.AreEqual(expected, SquadCommands.FindReinforceReason(dead, inRange, affordable, working));
        }

        [Test]
        public void SquadCommands_AreOnlyOfferedForSquads()
        {
            Refresh();
            var squad = _em.CreateEntity(typeof(SquadMember));
            var tank = _em.CreateEntity(typeof(EntityInfo));
            var source = new SquadCommands();

            var tankOnly = new List<CommandEntry>();
            source.Build(_context, new[] { tank }, tankOnly);
            var withSquad = new List<CommandEntry>();
            source.Build(_context, new[] { tank, squad }, withSquad);

            Assert.IsEmpty(tankOnly);
            Assert.AreEqual(new[] { 'R', 'T' }, withSquad.Select(entry => entry.Hotkey).ToArray());
        }

        [Test]
        public void MemberCard_ShowsItsWeapon_AndWorkOverCover()
        {
            var card = new SquadMemberCard();
            var digger = new SquadMember
            {
                Health = 10f, MaxHealth = 10f, Cover = CoverKind.Heavy, Action = MemberAction.Dig,
            };

            card.Show(digger, "M249");
            var label = card.Q<Label>(className: "hud-member__state");
            Assert.AreEqual("M249", card.Q<Label>(className: "hud-member__weapon").text);
            Assert.AreEqual(HUDText.Text("squad.digging"), label.text);

            digger.Health = 0f;
            card.Show(digger, "M249");
            Assert.AreEqual(HUDText.Text("squad.dead"), label.text);
            Assert.IsTrue(card.ClassListContains("hud-member--dead"));
        }
    }
}
