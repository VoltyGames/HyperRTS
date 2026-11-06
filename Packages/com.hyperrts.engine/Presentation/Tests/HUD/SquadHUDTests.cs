using HyperRTS.Simulation.Common.Infantry;
using System.Collections.Generic;
using System.Linq;
using HyperRTS.Presentation.HUD;
using HyperRTS.Presentation.HUD.Commands;
using HyperRTS.Presentation.HUD.Panels;
using HyperRTS.Simulation.Combat.Weapons;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Orders;
using HyperRTS.Simulation.Squads;
using NUnit.Framework;
using Unity.Entities;
using UnityEngine.InputSystem;
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
            var button = CommandTestKit.Button("Reinforce");

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
            var source = new SquadCommands(CommandTestKit.Orders(CommandType.Retreat, CommandType.Reinforce));

            var tankOnly = new List<CommandEntry>();
            source.Build(_context, new[] { tank }, tankOnly);
            var withSquad = new List<CommandEntry>();
            source.Build(_context, new[] { tank, squad }, withSquad);

            Assert.IsEmpty(tankOnly);
            Assert.AreEqual(new[] { Key.R, Key.T }, withSquad.Select(entry => entry.Hotkey).ToArray());
        }

        [Test]
        public void MemberCard_ShowsItsWeapon_AndWorkOverCover()
        {
            var card = new SquadMemberCard(HUDPanelTests.Layouts.SquadMemberCard);
            var digger = new SquadMember
            {
                Health = 10f, MaxHealth = 10f, Cover = CoverKind.Heavy, Action = MemberAction.Dig,
            };

            card.Show(digger, "M249");
            var label = card.Root.Q<Label>(className: "hud-member__state");
            Assert.AreEqual("M249", card.Root.Q<Label>(className: "hud-member__weapon").text);
            Assert.AreEqual(HUDText.Text("squad.digging"), label.text);

            digger.Health = 0f;
            card.Show(digger, "M249");
            Assert.AreEqual(HUDText.Text("squad.dead"), label.text);
            Assert.IsTrue(card.Root.ClassListContains("hud-member--dead"));
        }

        [Test]
        public void SquadView_NamesEachMemberByItsOwnWeapon_AndAnUpgradeByItsMount()
        {
            Refresh();
            _em.CreateSingleton(default(SquadRules));
            var squad = _em.CreateEntity(typeof(Suppression));
            var members = _em.AddBuffer<SquadMember>(squad);
            members.Add(new SquadMember { Health = 10f, MaxHealth = 10f, Designation = "M4A1" });
            members.Add(new SquadMember { Health = 10f, MaxHealth = 10f, Designation = "M249" });
            members.Add(new SquadMember { Health = 10f, MaxHealth = 10f, Designation = "M4A1", Weapon = 1 });
            var poses = _em.AddBuffer<WeaponPose>(squad);
            poses.Add(default);
            poses.Add(new WeaponPose { Designation = "AT4" });
            var root = new VisualElement();
            root.Add(new VisualElement { name = "members" });
            root.Add(new HUDBar { name = "suppression" });
            root.Add(new Label { name = "level" });
            root.Add(new VisualElement { name = "status" });
            root.Add(new Label { name = "status-text" });
            root.Add(new HUDBar { name = "progress" });

            new SquadView(root, HUDPanelTests.Layouts.SquadMemberCard).Refresh(_context, squad);

            var names = root.Query<Label>(className: "hud-member__weapon").ToList().Select(label => label.text);
            CollectionAssert.AreEqual(new[] { "M4A1", "M249", "AT4" }, names);
        }
    }
}
