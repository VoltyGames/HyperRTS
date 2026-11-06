using System.Collections.Generic;
using System.Linq;
using HyperRTS.Presentation.Interaction;
using HyperRTS.Presentation.HUD;
using HyperRTS.Presentation.HUD.Commands;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Orders;
using HyperRTS.Simulation.Production;
using HyperRTS.Simulation.Selection;
using NUnit.Framework;
using Unity.Entities;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace HyperRTS.Presentation.Tests.HUD
{
    /// <summary>Where the command card puts specs, how it pages past the grid, and what a hotkey press does.</summary>
    public class CommandCardTests : HUDFixture
    {
        private const int LastSlot = CommandCard.Slots - 1;

        private readonly List<string> _pressed = new();
        private readonly List<(CommandSpec Spec, string Label)> _specs = new();

        [SetUp]
        public void ClearSpecs()
        {
            _pressed.Clear();
            _specs.Clear();
        }

        [Test]
        public void Card_PlacesEntriesBySlot_WithBlanksBetween()
        {
            var card = NewCard(Spec("far", slot: 7), Spec("near"));

            Assert.AreEqual("far", Caption(card.Root[7]));
            Assert.AreEqual("near", Caption(card.Root[0]));
            Assert.IsTrue(Enumerable.Range(1, 6).All(i => card.Root[i].ClassListContains("hud-command--blank")));
        }

        [Test]
        public void Card_MovesATakenSlotToTheNextFree()
        {
            var card = NewCard(Spec("a"), Spec("b"));

            Assert.AreEqual("a", Caption(card.Root[0]));
            Assert.AreEqual("b", Caption(card.Root[1]));
        }

        [Test]
        public void Card_PagesWhatTheGridCannotHold_AndTheLastSlotTurnsThePage()
        {
            var specs = new List<CommandSpec>();
            for (var i = 0; i < CommandCard.Slots; i++)
            {
                specs.Add(Spec($"s{i}", slot: i));
            }

            specs.Add(Spec("s15", slot: CommandCard.Slots - 1));
            var card = NewCard(specs.ToArray());

            Assert.AreEqual("More", Caption(card.Root[LastSlot]));
            Assert.AreEqual("s13", Caption(card.Root[LastSlot - 1]));

            Turn(card);

            Assert.AreEqual("s14", Caption(card.Root[0]), "slot 14 is the page card's, so these spilled over");
            Assert.AreEqual("s15", Caption(card.Root[1]));
            Assert.AreEqual("Back", Caption(card.Root[LastSlot]));
        }

        [Test]
        public void Card_WithOnePage_HasNoPageCard()
        {
            var card = NewCard(Spec("only"));

            Assert.AreEqual(1, card.Root.Children().Count(child => child.ClassListContains("hud-command")));
        }

        [Test]
        public void Hotkey_PressesOneEntry_TheOneOnTheShownPage()
        {
            var card = NewCard(Spec("basic", Key.B), Spec("twin", Key.B), Spec("advanced", Key.B, page: 1));

            PressHotkey(card, Key.B);
            CollectionAssert.AreEqual(new[] { "basic" }, _pressed, "two on one page still act once");

            _pressed.Clear();
            Turn(card);
            PressHotkey(card, Key.B);
            CollectionAssert.AreEqual(new[] { "advanced" }, _pressed);
        }

        [Test]
        public void Hotkey_DoesNothingWhileTheEntryIsRefused()
        {
            var card = NewCard(Spec("basic", Key.B));
            var button = card.Root.Q<CommandButton>();

            button.ShowRefusal(PurchaseRefusal.CannotAfford);
            PressHotkey(card, Key.B);

            Assert.IsEmpty(_pressed);
            Assert.IsTrue(button.ClassListContains("hud-command--unaffordable"), "a refused button still shows why");
        }

        [Test]
        public void Back_EscapeAndANewSelection_TurnBackToTheFirstPage()
        {
            var card = NewCard(Spec("first"), Spec("second", page: 1));

            Turn(card);
            Assert.AreEqual("second", Caption(card.Root[0]));
            Turn(card);
            Assert.AreEqual("first", Caption(card.Root[0]), "Back");

            Turn(card);
            Assert.IsTrue(_view.CancelInteraction());
            Refreshed(card);
            Assert.AreEqual("first", Caption(card.Root[0]), "Escape");
            Assert.IsFalse(_view.CancelInteraction(), "nothing left to undo, so the front end may open its menu");

            Turn(card);
            _em.CreateEntity(typeof(Selected), typeof(EntityInfo));
            Refreshed(card);
            Assert.AreEqual("first", Caption(card.Root[0]), "a new selection");
            Assert.AreEqual(0, _context.CommandPage);
        }

        [Test]
        public void Cancel_UndoesPlacement_ThenTargeting_ThenThePage()
        {
            var state = new InteractionState
            {
                Placement = new PlacementState { Active = true },
                Pending = new PendingCommand { Type = CommandType.AttackMove },
                Page = new CommandCardPage { Page = 1 },
            };

            Assert.IsTrue(state.Cancel());
            Assert.IsFalse(state.Placement.Active);
            Assert.AreEqual(CommandType.AttackMove, state.Pending.Type);
            Assert.IsTrue(state.Cancel());
            Assert.AreEqual(CommandType.None, state.Pending.Type);
            Assert.AreEqual(1, state.Page.Page);
            Assert.IsTrue(state.Cancel());
            Assert.AreEqual(0, state.Page.Page);
            Assert.IsFalse(state.Cancel());
        }

        private CommandSpec Spec(string label, Key key = Key.None, int page = 0, int slot = 0)
        {
            var spec = CommandSpec.Create(CommandTarget.Order, key, page, slot);
            _specs.Add((spec, label));
            return spec;
        }

        private CommandCard NewCard(params CommandSpec[] specs)
        {
            var kit = CommandTestKit.Kit(specs);
            var card = new CommandCard(HUDPanelTests.Slot("commands"), HUDPanelTests.Layouts, kit.Catalogue,
                new SpecSource(kit, _specs, _pressed));
            return Refreshed(card);
        }

        private CommandCard Refreshed(CommandCard card)
        {
            Refresh();
            card.Refresh(_context);
            return card;
        }

        // Presses the page card, which turns the page on the next refresh.
        private void Turn(CommandCard card)
        {
            ((CommandButton)card.Root[LastSlot]).Press();
            Refreshed(card);
        }

        private void PressHotkey(CommandCard card, Key key)
        {
            var presses = new HotkeyPresses();
            presses.Add(key);
            using var query = _em.CreateEntityQuery(typeof(HotkeyPresses));
            query.SetSingleton(presses);
            card.Refresh(_context);
            query.SetSingleton(new HotkeyPresses());
        }

        private static string Caption(VisualElement button) => button.Q<Label>(className: "hud-command__caption").text;

        // Adds a text button for each spec, which records its label when pressed.
        private sealed class SpecSource : ICommandSource
        {
            private readonly CommandKit _kit;
            private readonly List<(CommandSpec Spec, string Label)> _specs;
            private readonly List<string> _pressed;

            public SpecSource(CommandKit kit, List<(CommandSpec Spec, string Label)> specs, List<string> pressed)
            {
                _kit = kit;
                _specs = specs;
                _pressed = pressed;
            }

            public void Build(HUDContext context, IReadOnlyList<Entity> owned, List<CommandEntry> entries)
            {
                foreach (var (spec, label) in _specs)
                {
                    entries.Add(_kit.Create(spec, label, null, () => _pressed.Add(label)));
                }
            }

            public void Refresh(HUDContext context)
            {
            }
        }
    }
}
