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

namespace HyperRTS.Simulation.Tests
{
    /// <summary>PickRanking over hand-built ray hits; identity view-projection maps world (x, y) to screen pixels.</summary>
    public class PickRankingTests
    {
        private static readonly float2 ScreenSize = new(1000f, 1000f);

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

        private Entity CreateUnit(float3 position)
        {
            var entity = CreateSelectable(position);
            _entityManager.AddComponent<UnitTag>(entity);
            _entityManager.AddComponentData(entity, new NavAgent { Radius = 0.001f });
            return entity;
        }

        private Entity CreateSelectable(float3 position)
        {
            var entity = _entityManager.CreateEntity();
            _entityManager.AddComponentData(entity, LocalTransform.FromPosition(position));
            _entityManager.AddComponent<Selectable>(entity);
            return entity;
        }

        private Entity Pick(float2 pointer, Entity[] units, params RaycastHit[] hits)
        {
            // Any system's state builds the lookups; the selection system is always in the test world.
            var world = _world.World.Unmanaged;
            ref var state = ref world.ResolveSystemStateRef(world.GetExistingUnmanagedSystem<SelectionSystem>());
            var ranking = new PickRanking(ref state);
            using var hitArray = new NativeArray<RaycastHit>(hits, Allocator.Temp);
            using var unitArray = new NativeArray<Entity>(units, Allocator.Temp);
            ranking.TryPick(hitArray, unitArray, pointer, float4x4.identity, ScreenSize, out var entity, out _);
            return entity;
        }

        private static RaycastHit Hit(Entity entity, float fraction) => new() { Entity = entity, Fraction = fraction };

        [Test]
        public void UnitBehindBuilding_WinsOverTheBuilding()
        {
            var building = CreateSelectable(float3.zero);
            var unit = CreateUnit(float3.zero);

            Assert.AreEqual(unit, Pick(new float2(500f, 500f), new[] { unit }, Hit(building, 0.2f), Hit(unit, 0.6f)));
        }

        [Test]
        public void UnitWithinTenPixels_WinsOverTheBuildingOnTheRay()
        {
            var building = CreateSelectable(float3.zero);
            var unit = CreateUnit(new float3(0.016f, 0f, 0f)); // 8 px right of the screen centre

            Assert.AreEqual(unit, Pick(new float2(500f, 500f), new[] { unit }, Hit(building, 0.2f)));
            Assert.AreEqual(building, Pick(new float2(480f, 500f), new[] { unit }, Hit(building, 0.2f)), "28 px is too far");
        }

        [Test]
        public void FogHiddenUnit_NeverWins()
        {
            var building = CreateSelectable(float3.zero);
            var unit = CreateUnit(float3.zero);
            _entityManager.AddComponent<FogHidden>(unit);

            Assert.AreEqual(building, Pick(new float2(500f, 500f), new[] { unit }, Hit(building, 0.2f), Hit(unit, 0.6f)));
        }

        [Test]
        public void NothingRanked_ReturnsTheClosestHit()
        {
            var far = CreateSelectable(float3.zero);
            var near = CreateSelectable(float3.zero);

            Assert.AreEqual(near, Pick(new float2(500f, 500f), new Entity[0], Hit(far, 0.7f), Hit(near, 0.3f)));
        }

        [Test]
        public void ClickOnEmptyGround_PicksNothingSelectable()
        {
            var ground = _entityManager.CreateEntity();
            var unit = CreateUnit(new float3(0.5f, 0.5f, 0f)); // far corner of the screen

            var picked = Pick(new float2(500f, 500f), new[] { unit }, Hit(ground, 0.5f));

            Assert.AreEqual(ground, picked);
            Assert.IsFalse(_entityManager.HasComponent<Selectable>(picked));
        }
    }
}
