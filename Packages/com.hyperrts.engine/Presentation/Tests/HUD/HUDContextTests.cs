using System.Collections.Generic;
using System.Linq;
using HyperRTS.Presentation.Interaction;
using HyperRTS.Presentation.Common;
using HyperRTS.Simulation.Buildings;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Common.Buildings;
using HyperRTS.Simulation.Common.Players;
using HyperRTS.Simulation.Orders;
using HyperRTS.Simulation.Selection;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine.UIElements;
using HyperRTS.Presentation.HUD.Commands;

namespace HyperRTS.Presentation.Tests.HUD
{
    /// <summary>The HUD's view of a hand-built world: selection, and which orders the card offers.</summary>
    public class HUDContextTests : HUDFixture
    {
        [Test]
        public void SelectionHash_ChangesWhenASelectedBuildingIsCapturedOrFinished()
        {
            var building = _em.CreateEntity(typeof(Selected), typeof(EntityInfo), typeof(ConstructionProgress));
            _em.AddComponentData(building, new Owner { Value = 2 });
            var built = Hash();

            _em.SetComponentData(building, new Owner { Value = 1 });
            var captured = Hash();
            _em.SetComponentEnabled<ConstructionProgress>(building, false);
            var finished = Hash();

            Assert.AreNotEqual(built, captured);
            Assert.AreNotEqual(captured, finished);
            Assert.AreEqual(finished, Hash(), "stable while nothing changes");
        }

        [Test]
        public void Observer_SeesTheMatch_WithoutALocalPlayer()
        {
            _em.DestroyEntity(_em.CreateEntityQuery(typeof(LocalPlayer)).GetSingletonEntity());
            _em.CreateSingleton(new OwnerRelations());

            Assert.IsTrue(_view.Refresh(_kit.World));
            Assert.IsFalse(_view.HasLocalPlayer);
            Assert.IsFalse(_view.IsLocalDefeated());
            Assert.IsTrue(_context.Refresh(_view));
        }

        [Test]
        public void ShownPlayer_IsTheLocalOneUntilAnotherIsPicked_AndOnlyTheLocalOneCommands()
        {
            Refresh();
            Assert.IsTrue(_context.HasShownPlayer);
            Assert.IsTrue(_context.CanCommand);

            var other = _kit.AddPlayer(2);
            _context.ShowPlayer(other);
            Refresh();

            Assert.AreEqual(other, _context.ShownPlayer);
            Assert.AreEqual(2, _context.ShownOwner);
            Assert.IsFalse(_context.CanCommand);
            Assert.DoesNotThrow(() => _context.Issue(default), "a command for a player you only watch is dropped");

            _context.ShowPlayer(Entity.Null);
            Refresh();
            Assert.AreEqual(_view.LocalPlayer, _context.ShownPlayer);
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
