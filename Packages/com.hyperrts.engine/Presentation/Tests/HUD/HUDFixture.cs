using HyperRTS.Presentation.Interaction;
using HyperRTS.Presentation.Common;
using HyperRTS.Presentation.HUD;
using NUnit.Framework;
using Unity.Entities;

namespace HyperRTS.Presentation.Tests.HUD
{
    /// <summary>A hand-built world with a local player and a HUD context, shared by the HUD tests.</summary>
    public abstract class HUDFixture
    {
        protected PresentationWorldKit _kit;
        protected EntityManager _em;
        protected MatchView _view;
        protected HUDContext _context;
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

        protected void Refresh()
        {
            _view.Refresh(_kit.World);
            Assert.IsTrue(_context.Refresh(_view));
        }
    }
}
