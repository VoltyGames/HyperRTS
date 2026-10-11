using System.Collections.Generic;
using HyperRTS.Client.Interaction;
using HyperRTS.Presentation.Common;
using HyperRTS.Presentation.HUD;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Selection;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Presentation.Tests
{
    /// <summary>The HUD's view of a world built by hand, without a scene or the default world.</summary>
    public class HUDContextTests
    {
        private World _world;
        private EntityManager _em;
        private MatchView _view;
        private HUDContext _context;

        [SetUp]
        public void SetUp()
        {
            _world = new World("HUD Tests");
            _em = _world.EntityManager;
            _world.CreateSystem<ClientSingletonSystem>();
            var local = _em.CreateEntity(typeof(LocalPlayer));
            _em.AddComponentData(local, new Player { Faction = 1 });
            _view = new MatchView();
            _context = new HUDContext();
        }

        [TearDown]
        public void TearDown() => _world.Dispose();

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

        private int Hash()
        {
            Refresh();
            return _context.SelectionHash;
        }

        [Test]
        public void Observer_SeesTheMatch_WithoutALocalPlayer()
        {
            _em.DestroyEntity(_em.CreateEntityQuery(typeof(LocalPlayer)).GetSingletonEntity());
            _em.CreateSingleton(new FactionRelations());

            Assert.IsTrue(_view.Refresh(_world));
            Assert.IsFalse(_view.HasLocalPlayer);
            Assert.IsFalse(_view.IsLocalDefeated());
            Assert.IsTrue(_context.Refresh(_view));
        }

        [Test]
        public void CommandCard_PlacesSlottedEntriesFirst_AndPressesHotkeys()
        {
            var source = new TestSource();
            var card = new CommandCard(source);
            Refresh();
            card.Refresh(_context);

            Assert.AreEqual(source.Slotted.Button, card.Root[0], "slot 0 comes before unslotted entries");
            Assert.AreEqual(0, source.Pressed);

            var presses = new HotkeyPresses();
            presses.Keys.Add('J');
            _em.CreateEntityQuery(typeof(HotkeyPresses)).SetSingleton(presses);
            card.Refresh(_context);

            Assert.AreEqual(1, source.Pressed);
        }

        private sealed class TestSource : ICommandSource
        {
            public CommandEntry Slotted;
            public int Pressed;

            public void Build(HUDContext context, IReadOnlyList<Entity> owned, List<CommandEntry> entries)
            {
                entries.Add(new CommandEntry("Later", () => { }));
                Slotted = new CommandEntry("Jam", () => Pressed++)
                {
                    Slot = 0,
                    Hotkey = 'j',
                };
                entries.Add(Slotted);
            }

            public void Refresh(HUDContext context)
            {
            }
        }

        private void Refresh()
        {
            _view.Refresh(_world);
            Assert.IsTrue(_context.Refresh(_view));
        }

        private T Singleton<T>() where T : unmanaged, IComponentData
        {
            using var query = _em.CreateEntityQuery(ComponentType.ReadOnly<T>());
            return query.GetSingleton<T>();
        }
    }
}
