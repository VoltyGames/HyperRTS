using HyperRTS.Simulation.Selection;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Tests
{
    /// <summary>Drag-box ownership preference and control groups, with the local player as faction 1.</summary>
    public class SelectionRulesTests
    {
        private static readonly float2 Screen = new(100f, 100f);

        private TestWorld _world;

        [SetUp]
        public void SetUp()
        {
            _world = new TestWorld();
            _world.CreateMatch(1, 2);
        }

        [TearDown]
        public void TearDown() => _world.Dispose();

        // Identity view-projection: world (x, y, 0) lands at ((x, y) * 0.5 + 0.5) * Screen.
        private void Send(SelectionInput input)
        {
            input.ViewProjection = float4x4.identity;
            input.ScreenSize = Screen;

            var singleton = _world.EntityManager.CreateEntityQuery(typeof(SelectionInput));
            singleton.SetSingleton(input);
            _world.Tick();
            singleton.SetSingleton(new SelectionInput());
        }

        private void DragAll(bool subtract = false) => Send(new SelectionInput
        {
            Command = SelectionCommand.DragRelease,
            DragMin = float2.zero,
            DragMax = Screen,
            Subtract = subtract,
        });

        private bool IsSelected(Entity entity) => _world.IsEnabled<Selected>(entity);

        private void SetSelected(Entity entity, bool value) =>
            _world.EntityManager.SetComponentEnabled<Selected>(entity, value);

        [Test]
        public void DragRank_OrdersOwnedUnitsOverOwnedBuildingsOverOthers()
        {
            Assert.AreEqual(2, SelectionMath.DragRank(true, true, false));
            Assert.AreEqual(1, SelectionMath.DragRank(true, false, true));
            Assert.AreEqual(0, SelectionMath.DragRank(false, true, false));
            Assert.AreEqual(0, SelectionMath.DragRank(true, false, false));
        }

        [Test]
        public void DragBox_WithOwnedUnits_SelectsOnlyThem()
        {
            var unit = _world.SpawnUnit(1, new float3(0f, 0f, 0f));
            var building = _world.SpawnBuilding(1, new float3(0.2f, 0f, 0f), new float2(2f, 2f));
            var enemy = _world.SpawnUnit(2, new float3(-0.2f, 0f, 0f));

            DragAll();

            Assert.IsTrue(IsSelected(unit));
            Assert.IsFalse(IsSelected(building));
            Assert.IsFalse(IsSelected(enemy));
        }

        [Test]
        public void DragBox_WithoutOwnedUnits_FallsBackToOwnedBuildings()
        {
            var building = _world.SpawnBuilding(1, new float3(0.2f, 0f, 0f), new float2(2f, 2f));
            var enemy = _world.SpawnUnit(2, new float3(-0.2f, 0f, 0f));

            DragAll();

            Assert.IsTrue(IsSelected(building));
            Assert.IsFalse(IsSelected(enemy));
        }

        [Test]
        public void DragBox_WithNothingOwned_SelectsEverything()
        {
            var enemy = _world.SpawnUnit(2, new float3(-0.2f, 0f, 0f));
            var enemyBuilding = _world.SpawnBuilding(2, new float3(0.2f, 0f, 0f), new float2(2f, 2f));

            DragAll();

            Assert.IsTrue(IsSelected(enemy));
            Assert.IsTrue(IsSelected(enemyBuilding));
        }

        [Test]
        public void DragBox_WithCtrl_RemovesEverythingInBox()
        {
            var unit = _world.SpawnUnit(1, new float3(0f, 0f, 0f));
            var building = _world.SpawnBuilding(1, new float3(0.2f, 0f, 0f), new float2(2f, 2f));
            SetSelected(unit, true);
            SetSelected(building, true);

            DragAll(subtract: true);

            Assert.IsFalse(IsSelected(unit));
            Assert.IsFalse(IsSelected(building));
        }

        [Test]
        public void ControlGroup_AssignThenRecall_RestoresOwnedSelection()
        {
            var first = _world.SpawnUnit(1, new float3(0f, 0f, 0f));
            var second = _world.SpawnUnit(1, new float3(0.5f, 0f, 0f));
            var enemy = _world.SpawnUnit(2, new float3(-0.5f, 0f, 0f));
            SetSelected(first, true);
            SetSelected(enemy, true);

            Send(new SelectionInput { Command = SelectionCommand.AssignGroup, Group = 2 });
            Assert.IsTrue(IsSelected(first), "assigning keeps the selection");

            SetSelected(first, false);
            SetSelected(enemy, false);
            SetSelected(second, true);
            Send(new SelectionInput { Command = SelectionCommand.RecallGroup, Group = 2 });

            Assert.IsTrue(IsSelected(first));
            Assert.IsFalse(IsSelected(second), "recall replaces the selection");
            Assert.IsFalse(IsSelected(enemy), "enemies never join a control group");

            Send(new SelectionInput { Command = SelectionCommand.RecallGroup, Group = 3 });
            Assert.IsFalse(IsSelected(first), "other groups are empty");
        }

        [Test]
        public void ControlGroup_Reassign_MovesMembership()
        {
            var first = _world.SpawnUnit(1, new float3(0f, 0f, 0f));
            var second = _world.SpawnUnit(1, new float3(0.5f, 0f, 0f));
            SetSelected(first, true);
            Send(new SelectionInput { Command = SelectionCommand.AssignGroup, Group = 0 });

            SetSelected(first, false);
            SetSelected(second, true);
            Send(new SelectionInput { Command = SelectionCommand.AssignGroup, Group = 0 });

            SetSelected(second, false);
            Send(new SelectionInput { Command = SelectionCommand.RecallGroup, Group = 0 });

            Assert.IsFalse(IsSelected(first));
            Assert.IsTrue(IsSelected(second));
        }
    }
}
