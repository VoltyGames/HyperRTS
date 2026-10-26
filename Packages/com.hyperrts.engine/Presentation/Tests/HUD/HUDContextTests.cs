using System.Collections.Generic;
using System.Linq;
using HyperRTS.Client.Interaction;
using HyperRTS.Presentation.Common;
using HyperRTS.Presentation.HUD;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Selection;
using HyperRTS.Simulation.Transport;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine.UIElements;

namespace HyperRTS.Presentation.Tests.HUD
{
    /// <summary>The HUD's view of a hand-built world and the command card it drives: slots, hotkeys and pages.</summary>
    public class HUDContextTests
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
            _kit = new PresentationWorldKit("HUD Tests");
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
        public void SelectionHash_ChangesWhenASelectedBuildingIsCapturedOrFinished()
        {
            var building = _em.CreateEntity(typeof(Selected), typeof(EntityInfo), typeof(ConstructionProgress));
            _em.AddComponentData(building, new Faction { Value = 2 });
            var built = Hash();

            _em.SetComponentData(building, new Faction { Value = 1 });
            var captured = Hash();
            _em.SetComponentEnabled<ConstructionProgress>(building, false);
            var finished = Hash();

            Assert.AreNotEqual(built, captured);
            Assert.AreNotEqual(captured, finished);
            Assert.AreEqual(finished, Hash(), "stable while nothing changes");
        }

        [Test]
        public void Writes_ReachTheClientSingletons()
        {
            Refresh();

            _context.FocusCamera(new float3(5f, 0f, 7f));
            _context.SetPointerOverUI(true);

            var focus = Singleton<CameraFocusRequest>();
            Assert.IsTrue(focus.Pending);
            Assert.AreEqual(new float3(5f, 0f, 7f), focus.Point);
            Assert.IsTrue(Singleton<PointerState>().OverUI);
        }

        [Test]
        public void Observer_SeesTheMatch_WithoutALocalPlayer()
        {
            _em.DestroyEntity(_em.CreateEntityQuery(typeof(LocalPlayer)).GetSingletonEntity());
            _em.CreateSingleton(new FactionRelations());

            Assert.IsTrue(_view.Refresh(_kit.World));
            Assert.IsFalse(_view.HasLocalPlayer);
            Assert.IsFalse(_view.IsLocalDefeated());
            Assert.IsTrue(_context.Refresh(_view));
        }

        [Test]
        public void UnitActions_OfferSell_OnlyForBuildingsNotAGarrisonItsOccupantsHold()
        {
            Refresh();
            var depot = _em.CreateEntity(typeof(BuildingTag));
            var house = _em.CreateEntity(typeof(BuildingTag), typeof(NeutralGarrison));
            var source = new UnitActionCommands();

            var garrisonOnly = new List<CommandEntry>();
            source.Build(_context, new[] { house }, garrisonOnly);
            var withDepot = new List<CommandEntry>();
            source.Build(_context, new[] { house, depot }, withDepot);

            Assert.IsEmpty(garrisonOnly, "a house you occupy isn't yours to sell");
            Assert.AreEqual(1, withDepot.Count);
        }

        [Test]
        public void CommandCard_PlacesSlottedEntriesFirst_AndPressesHotkeys()
        {
            var source = new SlottedSource();
            var card = Refreshed(new CommandCard(HUDPanelTests.Layouts, source));

            Assert.AreEqual(source.Slotted.Button, card.Root[0], "slot 0 comes before unslotted entries");
            Assert.AreEqual(0, source.Pressed);

            PressHotkey(card, 'J');

            Assert.AreEqual(1, source.Pressed);
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

        private int Hash()
        {
            Refresh();
            return _context.SelectionHash;
        }

        private void Refresh()
        {
            _view.Refresh(_kit.World);
            Assert.IsTrue(_context.Refresh(_view));
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

        private void PressHotkey(CommandCard card, char key)
        {
            var presses = new HotkeyPresses();
            presses.Keys.Add(key);
            using var query = _em.CreateEntityQuery(typeof(HotkeyPresses));
            query.SetSingleton(presses);
            card.Refresh(_context);
            query.SetSingleton(new HotkeyPresses());
        }

        private T Singleton<T>() where T : unmanaged, IComponentData
        {
            using var query = _em.CreateEntityQuery(ComponentType.ReadOnly<T>());
            return query.GetSingleton<T>();
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

        // An unslotted entry, then one in slot 0 with a hotkey.
        private sealed class SlottedSource : ICommandSource
        {
            public CommandEntry Slotted;
            public int Pressed;

            public void Build(HUDContext context, IReadOnlyList<Entity> owned, List<CommandEntry> entries)
            {
                entries.Add(new CommandEntry("Later", () => { }));
                Slotted = new CommandEntry("Jam", () => Pressed++) { Slot = 0, Hotkey = 'j' };
                entries.Add(Slotted);
            }

            public void Refresh(HUDContext context)
            {
            }
        }
    }
}
