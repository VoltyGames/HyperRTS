using System.Collections.Generic;
using System.Linq;
using HyperRTS.Client.Interaction;
using HyperRTS.Presentation.Common;
using HyperRTS.Presentation.HUD;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Selection;
using NUnit.Framework;
using Unity.Entities;
using UnityEngine.UIElements;

namespace HyperRTS.Presentation.Tests
{
    /// <summary>The command card's second page: what each page shows, the More and Back cards and turning back.</summary>
    public class CommandCardPagesTests
    {
        private const int LastSlot = CommandCard.Slots - 1;

        private PresentationWorldKit _kit;
        private EntityManager _em;
        private MatchView _view;
        private HUDContext _context;
        private HUDText _text;

        [SetUp]
        public void SetUp()
        {
            // A game's translated HUDText outlives Play mode (no domain reload); these tests read the engine's keys.
            _text = HUDText.Current;
            HUDText.Current = new HUDText();
            _kit = new PresentationWorldKit("Command Card Pages Tests");
            _em = _kit.EntityManager;
            _kit.World.CreateSystem<ClientSingletonSystem>();
            _kit.AddPlayer(1, local: true);
            _view = new MatchView();
            _context = new HUDContext();
        }

        [TearDown]
        public void TearDown()
        {
            HUDText.Current = _text;
            _kit.Dispose();
        }

        [Test]
        public void FirstPage_ShowsBasicAndOtherEntries_WithMoreInTheLastSlot()
        {
            var source = new PagedSource();
            var card = Refreshed(new CommandCard(HUDPanelTests.Layouts, source, new OtherSource()));

            Assert.AreEqual(source.Basic.Button, card.Root[0]);
            Assert.AreEqual("Stance", ((CommandButton)card.Root[1]).text, "other sources stay on page one");
            Assert.IsFalse(Shows(card, source.Advanced));
            Assert.AreEqual("More", Caption(card.Root[LastSlot]));
        }

        [Test]
        public void More_ShowsTheAdvancedPage_WithBackInTheSameSlot_AndItsHotkeys()
        {
            var source = new PagedSource();
            var card = Refreshed(new CommandCard(HUDPanelTests.Layouts, source, new OtherSource()));

            Turn(card);

            Assert.AreEqual(source.Advanced.Button, card.Root[0]);
            Assert.IsFalse(Shows(card, source.Basic));
            Assert.IsFalse(card.Root.Children().OfType<CommandButton>().Any(button => button.text == "Stance"));
            Assert.AreEqual("Back", Caption(card.Root[LastSlot]));

            PressHotkey(card, 'B');
            Assert.AreEqual(1, source.AdvancedPresses, "the shared key presses the shown page's entry");
            Assert.AreEqual(0, source.BasicPresses);
        }

        [Test]
        public void Back_EscapeAndANewSelection_TurnBackToTheFirstPage()
        {
            var source = new PagedSource();
            var card = Refreshed(new CommandCard(HUDPanelTests.Layouts, source));

            Turn(card);
            Turn(card);
            Assert.AreEqual(source.Basic.Button, card.Root[0], "Back");

            Turn(card);
            Assert.IsTrue(_view.CancelInteraction());
            Refreshed(card);
            Assert.AreEqual(source.Basic.Button, card.Root[0], "Escape");
            Assert.IsFalse(_view.CancelInteraction(), "nothing left to undo, so the front end may open its menu");

            Turn(card);
            _em.CreateEntity(typeof(Selected), typeof(EntityInfo));
            Refreshed(card);
            Assert.AreEqual(source.Basic.Button, card.Root[0], "a new selection");
            Assert.AreEqual(0, _context.CommandPage);
        }

        [Test]
        public void WithoutAdvancedEntries_ThereIsNoMoreCard()
        {
            var card = Refreshed(new CommandCard(HUDPanelTests.Layouts, new OtherSource()));

            Assert.AreEqual(1, card.Root.Children().Count(child => child.ClassListContains("hud-command")));
        }

        private CommandCard Refreshed(CommandCard card)
        {
            _view.Refresh(_kit.World);
            Assert.IsTrue(_context.Refresh(_view));
            card.Refresh(_context);
            return card;
        }

        // Presses the page card, which turns the page on the next refresh.
        private void Turn(CommandCard card)
        {
            ((CommandButton)card.Root[LastSlot]).Press();
            Refreshed(card);
        }

        private void PressHotkey(CommandCard card, char key)
        {
            var presses = new HotkeyPresses();
            presses.Keys.Add(key);
            using var query = _em.CreateEntityQuery(typeof(HotkeyPresses));
            query.SetSingleton(presses);
            card.Refresh(_context);
            query.SetSingleton(new HotkeyPresses());
        }

        private static bool Shows(CommandCard card, CommandEntry entry) => card.Root.Contains(entry.Button);

        private static string Caption(VisualElement card) => card.Q<Label>(className: "hud-command__caption").text;

        private sealed class PagedSource : ICommandSource
        {
            public CommandEntry Basic;
            public CommandEntry Advanced;
            public int BasicPresses;
            public int AdvancedPresses;

            public void Build(HUDContext context, IReadOnlyList<Entity> owned, List<CommandEntry> entries)
            {
                Basic = new CommandEntry("Barracks", () => BasicPresses++) { Hotkey = 'b' };
                Advanced = new CommandEntry("Airfield", () => AdvancedPresses++) { Hotkey = 'b', Page = 1 };
                entries.Add(Basic);
                entries.Add(Advanced);
            }

            public void Refresh(HUDContext context)
            {
            }
        }

        private sealed class OtherSource : ICommandSource
        {
            public void Build(HUDContext context, IReadOnlyList<Entity> owned, List<CommandEntry> entries) =>
                entries.Add(new CommandEntry("Stance", () => { }));

            public void Refresh(HUDContext context)
            {
            }
        }
    }
}
