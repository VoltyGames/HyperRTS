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
        [TestCase(SquadRefusal.NoPoint, "hud-command--no-point")]
        [TestCase(SquadRefusal.FullStrength, "hud-command--full-strength")]
        [TestCase(SquadRefusal.TooPoor, "hud-command--too-poor")]
        public void SquadButton_ShowsItsOwnRefusalClass(SquadRefusal refusal, string expected)
        {
            var button = new CommandEntry("Reinforce", () => { }).Button;

            button.ShowSquadRefusal(refusal);
            Assert.IsTrue(button.ClassListContains(expected));
            Assert.IsTrue(button.IsRefused);

            button.ShowSquadRefusal(SquadRefusal.None);
            Assert.IsFalse(button.ClassListContains(expected));
            Assert.IsFalse(button.IsRefused);
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
