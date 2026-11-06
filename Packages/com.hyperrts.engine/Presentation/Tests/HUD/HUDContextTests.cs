using System.Collections.Generic;
using System.Linq;
using HyperRTS.Presentation.Interaction;
using HyperRTS.Presentation.Common;
using HyperRTS.Presentation.HUD;
using HyperRTS.Simulation.Buildings;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Orders;
using HyperRTS.Simulation.Selection;
using HyperRTS.Simulation.Transport;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine.UIElements;
using HyperRTS.Presentation.HUD.Commands;
using HyperRTS.Presentation.HUD.Panels;

namespace HyperRTS.Presentation.Tests.HUD
{
    /// <summary>The HUD's view of a hand-built world: selection, and which orders the card offers.</summary>
    public class HUDContextTests : HUDFixture
    {
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
        public void SellCommand_Offered_OnlyForBuildingsNotAGarrisonItsOccupantsHold()
        {
            Refresh();
            var depot = _em.CreateEntity(typeof(BuildingTag));
            var house = _em.CreateEntity(typeof(BuildingTag), typeof(NeutralGarrison));
            var source = new OrderCommands(CommandTestKit.Orders(CommandType.Sell));

            var garrisonOnly = new List<CommandEntry>();
            source.Build(_context, new[] { house }, garrisonOnly);
            var withDepot = new List<CommandEntry>();
            source.Build(_context, new[] { house, depot }, withDepot);

            Assert.IsEmpty(garrisonOnly, "a house you occupy isn't yours to sell");
            Assert.AreEqual(1, withDepot.Count);
        }

        private int Hash()
        {
            Refresh();
            return _context.SelectionHash;
        }

        private T Singleton<T>() where T : unmanaged, IComponentData
        {
            using var query = _em.CreateEntityQuery(ComponentType.ReadOnly<T>());
            return query.GetSingleton<T>();
        }
    }
}
