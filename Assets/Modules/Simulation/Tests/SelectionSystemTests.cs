using HyperRTS.Core;
using HyperRTS.Simulation.Selection;
using NUnit.Framework;
using Unity.Collections;
using Unity.Core;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.Tests
{
    /// <summary>
    /// Pure-math cases need no world; box-select cases run <see cref="SelectionSystem"/> in an isolated
    /// world with an injected <see cref="SelectionInput"/> singleton (click raycast is checked in Play mode).
    /// </summary>
    public class SelectionSystemTests
    {
        private World _world;
        private EntityManager _entityManager;

        [SetUp]
        public void SetUp()
        {
            _world = new World("HyperRTS Selection Test World");
            _entityManager = _world.EntityManager;

            DefaultWorldInitialization.AddSystemsToRootLevelSystemGroups(_world,
                typeof(SimulationSystemGroup),
                typeof(TransformSystemGroup), // ordering target of MovementSystemGroup
                typeof(OrderSystemGroup),
                typeof(MovementSystemGroup),
                typeof(CombatSystemGroup),
                typeof(ProductionSystemGroup),
                typeof(LifecycleSystemGroup),
                typeof(SelectionSystem));
        }

        [TearDown]
        public void TearDown()
        {
            if (_world != null && _world.IsCreated)
            {
                _world.Dispose();
            }

            _world = null;
        }

        private void Tick(float deltaTime)
        {
            _world.SetTime(new TimeData(elapsedTime: deltaTime, deltaTime: deltaTime));
            _world.GetExistingSystemManaged<SimulationSystemGroup>().Update();
        }

        private Entity CreateSelectable(float3 position, SelectableKind kind = SelectableKind.Unit)
        {
            var entity = _entityManager.CreateEntity();
            _entityManager.AddComponentData(entity, LocalTransform.FromPosition(position));
            SelectionComponents.AddTo(_entityManager, entity, kind);
            return entity;
        }

        // Drives one drag-box gesture through the singleton; identity view-projection so a world point
        // (x, y, 0) maps to screen ((x, y) * 0.5 + 0.5) * screenSize.
        private void DragBox(float2 min, float2 max, float2 screenSize, bool additive = false, bool subtract = false)
        {
            var input = new SelectionInput
            {
                Command = SelectionCommand.DragRelease,
                DragMin = min,
                DragMax = max,
                ViewProjection = float4x4.identity,
                ScreenSize = screenSize,
                Additive = additive,
                Subtract = subtract,
            };

            if (!_entityManager.CreateEntityQuery(typeof(SelectionInput)).TryGetSingletonEntity<SelectionInput>(out var e))
            {
                e = _entityManager.CreateEntity(typeof(SelectionInput));
            }

            _entityManager.SetComponentData(e, input);
            Tick(0.1f);
        }

        // ---- Pure math -----------------------------------------------------------------------

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

            // A view-projection whose clip.w = -z, so a point with z > 0 is behind the camera.
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
            // Replace: selected iff hit.
            Assert.IsTrue(SelectionMath.ResolveSelected(false, true, false, false));
            Assert.IsFalse(SelectionMath.ResolveSelected(true, false, false, false));

            // Additive (Shift): keep current, add hits.
            Assert.IsTrue(SelectionMath.ResolveSelected(true, false, true, false));
            Assert.IsTrue(SelectionMath.ResolveSelected(false, true, true, false));

            // Subtract (Ctrl): remove hits, keep the rest. Subtract wins over additive.
            Assert.IsFalse(SelectionMath.ResolveSelected(true, true, false, true));
            Assert.IsTrue(SelectionMath.ResolveSelected(true, false, false, true));
            Assert.IsFalse(SelectionMath.ResolveSelected(true, true, true, true));
        }

        // ---- Box-select through the system ---------------------------------------------------

        [Test]
        public void DragBox_SelectsEntitiesInsideRect_AndIsEcsQueryable()
        {
            var screen = new float2(100, 100);
            var inside = CreateSelectable(new float3(0, 0, 0));   // -> screen (50, 50)
            var outside = CreateSelectable(new float3(1, 1, 0));  // -> screen (100, 100)

            DragBox(new float2(40, 40), new float2(60, 60), screen);

            Assert.IsTrue(_entityManager.IsComponentEnabled<Selected>(inside));
            Assert.IsFalse(_entityManager.IsComponentEnabled<Selected>(outside));

            // The selection set is an ECS query other systems can read (enabled bit honoured).
            using var selectedQuery = _entityManager.CreateEntityQuery(ComponentType.ReadOnly<Selected>());
            Assert.AreEqual(1, selectedQuery.CalculateEntityCount());
            using var selected = selectedQuery.ToEntityArray(Allocator.Temp);
            Assert.AreEqual(inside, selected[0]);
        }

        [Test]
        public void DragBox_WithoutModifier_ReplacesPreviousSelection()
        {
            var screen = new float2(100, 100);
            var entity = CreateSelectable(new float3(0, 0, 0));
            _entityManager.SetComponentEnabled<Selected>(entity, true);

            // Box far away from everything clears the selection.
            DragBox(new float2(1000, 1000), new float2(2000, 2000), screen);

            Assert.IsFalse(_entityManager.IsComponentEnabled<Selected>(entity));
        }

        [Test]
        public void DragBox_WithShift_AddsToSelection()
        {
            var screen = new float2(100, 100);
            var first = CreateSelectable(new float3(0, 0, 0));    // -> (50, 50)
            var second = CreateSelectable(new float3(0.5f, 0.5f, 0)); // -> (75, 75)
            _entityManager.SetComponentEnabled<Selected>(first, true);

            DragBox(new float2(70, 70), new float2(80, 80), screen, additive: true);

            Assert.IsTrue(_entityManager.IsComponentEnabled<Selected>(first), "Shift keeps the prior selection");
            Assert.IsTrue(_entityManager.IsComponentEnabled<Selected>(second));
        }

        [Test]
        public void DragBox_WithCtrl_RemovesFromSelection()
        {
            var screen = new float2(100, 100);
            var keep = CreateSelectable(new float3(0, 0, 0));     // -> (50, 50)
            var remove = CreateSelectable(new float3(0.5f, 0.5f, 0)); // -> (75, 75)
            _entityManager.SetComponentEnabled<Selected>(keep, true);
            _entityManager.SetComponentEnabled<Selected>(remove, true);

            DragBox(new float2(70, 70), new float2(80, 80), screen, subtract: true);

            Assert.IsTrue(_entityManager.IsComponentEnabled<Selected>(keep));
            Assert.IsFalse(_entityManager.IsComponentEnabled<Selected>(remove));
        }
    }
}
