using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Navigation;
using HyperRTS.Simulation.Selection;
using HyperRTS.Simulation.Vision;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.Tests.Orders
{
    /// <summary>
    /// Click picking: the ray against turned pick boxes and the ranking of what it hits. An identity view-projection
    /// maps world (x, y, 0) to ((x, y) * 0.5 + 0.5) * screen size.
    /// </summary>
    public class PickTests
    {
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
            return entity;
        }

        private Entity CreatePickableUnit(float3 position)
        {
            var entity = CreateSelectable(position);
            _entityManager.AddComponent<UnitTag>(entity);
            _entityManager.AddComponentData(entity, new NavAgent { Radius = 0.001f });
            return entity;
        }

        // Any system's state builds the lookups; the selection system is always in the test world.
        private ref SystemState SelectionState()
        {
            var world = _world.World.Unmanaged;
            return ref world.ResolveSystemStateRef(world.GetExistingUnmanagedSystem<SelectionSystem>());
        }

        private Entity Pick(float2 pointer, params PickHit[] hits)
        {
            ref var state = ref SelectionState();
            var ranking = new PickRanking(ref state);
            using var hitArray = new NativeArray<PickHit>(hits, Allocator.Temp);
            ranking.TryRank(hitArray, pointer, float4x4.identity, PickScreen, out var entity, out _);
            return entity;
        }

        private static PickHit Hit(Entity entity, float fraction) => new() { Entity = entity, Fraction = fraction };

        // Straight down from 10 m up.
        private bool Cast(float3 above, float2 pointer, out Entity entity, out float3 point)
        {
            ref var state = ref SelectionState();
            var ranking = new PickRanking(ref state);
            var ray = new PickRay(above + new float3(0f, 10f, 0f), new float3(0f, -1f, 0f), 20f);
            return ranking.TryPick(ref state, in ray, pointer, float4x4.identity, PickScreen, out entity, out point);
        }

        // Far from every unit on screen, so only the ray can pick.
        private bool Cast(float3 above, out Entity entity, out float3 point) =>
            Cast(above, new float2(-1000f, -1000f), out entity, out point);

        private Entity AddPickBox(Entity entity, float3 size)
        {
            _entityManager.AddComponentData(entity, new PickBox { HalfSize = size * 0.5f });
            return entity;
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

        [Test]
        public void Pick_HitsATurnedBox_AndMissesJustBesideIt()
        {
            // 4 m long on X, turned a quarter so it lies along Z, standing 2 m tall.
            var building = AddPickBox(CreateSelectable(float3.zero), new float3(4f, 2f, 1f));
            _entityManager.SetComponentData(building,
                LocalTransform.FromPositionRotation(float3.zero, quaternion.RotateY(math.PI / 2f)));

            Assert.IsTrue(Cast(new float3(0f, 0f, 1.9f), out var entity, out var point), "along its turned length");
            Assert.AreEqual(building, entity);
            Assert.AreEqual(1f, point.y, 1e-4f, "enters through the top");
            Assert.IsFalse(Cast(new float3(0f, 0f, 2.1f), out _, out _), "just past its end");
            Assert.IsFalse(Cast(new float3(0.6f, 0f, 0f), out _, out _), "just beside its turned width");
        }

        [Test]
        public void Pick_NeverPicksAUnitInsideAContainer()
        {
            var unit = AddPickBox(CreatePickableUnit(float3.zero), new float3(1f, 2f, 1f));
            _entityManager.AddComponentData(unit, new Inside { Container = Entity.Null });

            Assert.IsFalse(Cast(float3.zero, new float2(500f, 500f), out _, out _), "neither by ray nor on screen");

            _entityManager.SetComponentEnabled<Inside>(unit, false);
            Assert.IsTrue(Cast(float3.zero, out var entity, out _));
            Assert.AreEqual(unit, entity, "picked once it gets out");
        }
    }
}
