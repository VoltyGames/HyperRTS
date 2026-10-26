using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Navigation;
using HyperRTS.Simulation.Selection;
using HyperRTS.Simulation.Vision;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

namespace HyperRTS.Simulation.Tests.Orders
{
    /// <summary>
    /// Selection math, drag boxes with modifiers and ownership preference, control groups and click pick ranking.
    /// An identity view-projection maps world (x, y, 0) to ((x, y) * 0.5 + 0.5) * screen size.
    /// </summary>
    public class SelectionTests
    {
        private static readonly float2 Screen = new(100f, 100f);
        private static readonly float2 PickScreen = new(1000f, 1000f);

        private TestWorld _world;
        private EntityManager _entityManager;

        [SetUp]
        public void SetUp()
        {
            _world = new TestWorld();
            _entityManager = _world.EntityManager;
        }

        [TearDown]
        public void TearDown() => _world.Dispose();

        private Entity CreateSelectable(float3 position)
        {
            var entity = _entityManager.CreateEntity();
            _entityManager.AddComponentData(entity, LocalTransform.FromPosition(position));
            _entityManager.AddComponent<Selectable>(entity);
            _entityManager.AddComponent<Selected>(entity);
            _entityManager.SetComponentEnabled<Selected>(entity, false);
            return entity;
        }

        private Entity CreatePickableUnit(float3 position)
        {
            var entity = CreateSelectable(position);
            _entityManager.AddComponent<UnitTag>(entity);
            _entityManager.AddComponentData(entity, new NavAgent { Radius = 0.001f });
            return entity;
        }

        private void Send(SelectionInput input)
        {
            input.ViewProjection = float4x4.identity;
            input.ScreenSize = Screen;
            using var singleton = _entityManager.CreateEntityQuery(typeof(SelectionInput));
            singleton.SetSingleton(input);
            _world.Tick();
            singleton.SetSingleton(new SelectionInput());
        }

        private void Drag(float2 min, float2 max, bool additive = false, bool subtract = false) => Send(new SelectionInput
        {
            Command = SelectionCommand.DragRelease, DragMin = min, DragMax = max, Additive = additive, Subtract = subtract,
        });

        private void DragAll(bool subtract = false) => Drag(float2.zero, Screen, subtract: subtract);

        private bool IsSelected(Entity entity) => _world.IsEnabled<Selected>(entity);

        private void SetSelected(Entity entity, bool value) => _entityManager.SetComponentEnabled<Selected>(entity, value);

        private Entity Pick(float2 pointer, params RaycastHit[] hits)
        {
            // Any system's state builds the lookups; the selection system is always in the test world.
            var world = _world.World.Unmanaged;
            ref var state = ref world.ResolveSystemStateRef(world.GetExistingUnmanagedSystem<SelectionSystem>());
            var ranking = new PickRanking(ref state);
            using var hitArray = new NativeArray<RaycastHit>(hits, Allocator.Temp);
            ranking.TryRank(hitArray, pointer, float4x4.identity, PickScreen, out var entity, out _);
            return entity;
        }

        private static RaycastHit Hit(Entity entity, float fraction) => new() { Entity = entity, Fraction = fraction };

        [Test]
        public void RectContains_HandlesInsideOutsideEdgesAndInvertedCorners()
        {
            Assert.IsTrue(SelectionMath.RectContains(new float2(0, 0), new float2(10, 10), new float2(5, 5)));
            Assert.IsTrue(SelectionMath.RectContains(new float2(0, 0), new float2(10, 10), new float2(0, 10)), "edges inclusive");
            Assert.IsTrue(SelectionMath.RectContains(new float2(10, 10), new float2(0, 0), new float2(5, 5)), "corners in any order");
            Assert.IsFalse(SelectionMath.RectContains(new float2(0, 0), new float2(10, 10), new float2(11, 5)));
            Assert.IsTrue(SelectionMath.RectContains(new float2(3, 3), new float2(3, 3), new float2(3, 3)), "zero-size contains its point");
        }

        [Test]
        public void WorldToScreenPoint_MapsNdcToPixelsAndRejectsBehindCamera()
        {
            var screen = new float2(1920, 1080);

            Assert.IsTrue(SelectionMath.WorldToScreenPoint(float4x4.identity, float3.zero, screen, out var center));
            Assert.AreEqual(960f, center.x, 1e-3f);
            Assert.AreEqual(540f, center.y, 1e-3f);

            Assert.IsTrue(SelectionMath.WorldToScreenPoint(float4x4.identity, new float3(0.5f, -0.5f, 0f), screen, out var off));
            Assert.AreEqual(1440f, off.x, 1e-3f);
            Assert.AreEqual(270f, off.y, 1e-3f);

            // clip.w = -z, so a point with z > 0 is behind the camera.
            var behind = new float4x4(
                new float4(1, 0, 0, 0),
                new float4(0, 1, 0, 0),
                new float4(0, 0, 1, -1),
                new float4(0, 0, 0, 0));
            Assert.IsFalse(SelectionMath.WorldToScreenPoint(behind, new float3(0, 0, 5f), screen, out _));
        }

        [Test]
        public void ResolveSelected_AppliesModifierRules()
        {
            Assert.IsTrue(SelectionMath.ResolveSelected(false, true, false, false), "replace: selected if hit");
            Assert.IsFalse(SelectionMath.ResolveSelected(true, false, false, false));
            Assert.IsTrue(SelectionMath.ResolveSelected(true, false, true, false), "additive keeps the rest");
            Assert.IsTrue(SelectionMath.ResolveSelected(false, true, true, false));
            Assert.IsFalse(SelectionMath.ResolveSelected(true, true, false, true), "subtract removes hits");
            Assert.IsTrue(SelectionMath.ResolveSelected(true, false, false, true));
            Assert.IsFalse(SelectionMath.ResolveSelected(true, true, true, true), "subtract wins over additive");
        }

        [Test]
        public void DragBox_SelectsOnlyWhatIsInside_ReplacingTheSelection()
        {
            var inside = CreateSelectable(new float3(0, 0, 0));
            var outside = CreateSelectable(new float3(1, 1, 0));
            SetSelected(outside, true);

            Drag(new float2(40, 40), new float2(60, 60));

            Assert.IsTrue(IsSelected(inside));
            Assert.IsFalse(IsSelected(outside), "a drag without modifiers replaces the selection");
            using var selectedQuery = _entityManager.CreateEntityQuery(ComponentType.ReadOnly<Selected>());
            using var selected = selectedQuery.ToEntityArray(Allocator.Temp);
            CollectionAssert.AreEqual(new[] { inside }, selected.ToArray());
        }

        [Test]
        public void DragBox_WithShift_AddsToSelection()
        {
            var first = CreateSelectable(new float3(0, 0, 0));
            var second = CreateSelectable(new float3(0.5f, 0.5f, 0));
            SetSelected(first, true);

            Drag(new float2(70, 70), new float2(80, 80), additive: true);

            Assert.IsTrue(IsSelected(first), "Shift keeps the prior selection");
            Assert.IsTrue(IsSelected(second));
        }

        [Test]
        public void DragBox_WithOwnedUnits_SelectsOnlyThem()
        {
            _world.CreateMatch(1, 2);
            var unit = _world.SpawnUnit(1, new float3(0f, 0f, 0f));
            var building = _world.SpawnBuilding(1, new float3(0.2f, 0f, 0f), new float2(2f, 2f));
            var enemy = _world.SpawnUnit(2, new float3(-0.2f, 0f, 0f));

            DragAll();

            Assert.IsTrue(IsSelected(unit));
            Assert.IsFalse(IsSelected(building));
            Assert.IsFalse(IsSelected(enemy));
        }

        [Test]
        public void DragBox_WithoutOwnedUnits_FallsBackToOwnedBuildings_ThenToEverything()
        {
            _world.CreateMatch(1, 2);
            var building = _world.SpawnBuilding(1, new float3(0.2f, 0f, 0f), new float2(2f, 2f));
            var enemy = _world.SpawnUnit(2, new float3(-0.2f, 0f, 0f));
            var enemyBuilding = _world.SpawnBuilding(2, new float3(0.4f, 0f, 0f), new float2(2f, 2f));

            DragAll();
            Assert.IsTrue(IsSelected(building));
            Assert.IsFalse(IsSelected(enemy));

            _entityManager.DestroyEntity(building);
            DragAll();
            Assert.IsTrue(IsSelected(enemy), "with nothing owned, everything in the box");
            Assert.IsTrue(IsSelected(enemyBuilding));
        }

        [Test]
        public void DragBox_WithCtrl_RemovesEverythingInBox()
        {
            _world.CreateMatch(1, 2);
            var unit = _world.SpawnUnit(1, new float3(0f, 0f, 0f));
            var building = _world.SpawnBuilding(1, new float3(0.2f, 0f, 0f), new float2(2f, 2f));
            SetSelected(unit, true);
            SetSelected(building, true);

            DragAll(subtract: true);

            Assert.IsFalse(IsSelected(unit));
            Assert.IsFalse(IsSelected(building), "the ownership preference doesn't spare buildings");
        }

        [Test]
        public void ControlGroup_AssignThenRecall_RestoresOwnedSelection()
        {
            _world.CreateMatch(1, 2);
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
            _world.CreateMatch(1, 2);
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

        [Test]
        public void Pick_PrefersAUnitBehindOrBesideABuilding_UnlessFogHidesIt()
        {
            var building = CreateSelectable(float3.zero);
            var behind = CreatePickableUnit(float3.zero);
            Assert.AreEqual(behind, Pick(new float2(500f, 500f), Hit(building, 0.2f), Hit(behind, 0.6f)));

            _entityManager.AddComponent<FogHidden>(behind);
            Assert.AreEqual(building, Pick(new float2(500f, 500f), Hit(building, 0.2f), Hit(behind, 0.6f)),
                "a fog-hidden unit never wins");

            var beside = CreatePickableUnit(new float3(0.016f, 0f, 0f));
            Assert.AreEqual(beside, Pick(new float2(500f, 500f), Hit(building, 0.2f)), "8 px away");
            Assert.AreEqual(building, Pick(new float2(480f, 500f), Hit(building, 0.2f)), "28 px is too far");
        }

        [Test]
        public void Pick_WithNothingRanked_ReturnsTheClosestHit_EvenIfNotSelectable()
        {
            var far = CreateSelectable(float3.zero);
            var near = CreateSelectable(float3.zero);
            Assert.AreEqual(near, Pick(new float2(500f, 500f), Hit(far, 0.7f), Hit(near, 0.3f)));

            var ground = _entityManager.CreateEntity();
            CreatePickableUnit(new float3(0.5f, 0.5f, 0f));
            Assert.AreEqual(ground, Pick(new float2(500f, 500f), Hit(ground, 0.5f)), "empty ground picks nothing selectable");
        }
    }
}
