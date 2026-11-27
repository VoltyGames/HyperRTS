using System.Collections.Generic;
using HyperRTS.Presentation.Rendering;
using HyperRTS.Presentation.Rendering.LightTransport;
using HyperRTS.Presentation.Rendering.Parts;
using HyperRTS.Presentation.Rendering.Silhouettes;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Common.Authoring;
using HyperRTS.Simulation.Common.Combat;
using HyperRTS.Simulation.Destruction;
using HyperRTS.Simulation.Transport;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Entities.Graphics;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;

namespace HyperRTS.Presentation.Tests.Rendering.Parts
{
    /// <summary>What draws: baking for hiding and unit layers, visual prefabs, model variants and garrison parts.</summary>
    public class RenderingTests
    {
        private static readonly ModelVariants Three = new() { Count = 3, Shown = -1 };

        private static readonly float3 Place = new(10f, 0f, 5f);

        private World _world;
        private EntityManager _em;

        [SetUp]
        public void SetUp()
        {
            _world = new World("Rendering Tests");
            _em = _world.EntityManager;
        }

        [TearDown]
        public void TearDown() => _world.Dispose();

        [Test]
        public void BrokenModel_BakesAsAFullSizePrefab_WithItsChildMeshes()
        {
            var building = _em.CreateEntity();
            var intact = Piece(1f, building);
            var broken = Piece(0f, building);
            var shard = Piece(1f, broken);
            _em.AddBuffer<SectionModel>(building).Add(new SectionModel { Intact = intact, Broken = broken });

            Update<ModelCopyBakingSystem>();

            Assert.AreEqual(1f, _em.GetComponentData<LocalTransform>(broken).Scale);
            var place = _em.GetComponentData<LocalToWorld>(shard);
            Assert.AreEqual(1f, math.length(place.Value.c0.xyz), 1e-5f, "a copy draws its first frame at full size");
            Assert.AreEqual(Place, place.Position);
            Assert.IsTrue(_em.HasComponent<Prefab>(broken) && _em.HasComponent<Prefab>(shard));
            CollectionAssert.AreEqual(new[] { broken, shard }, Group(broken));
            CollectionAssert.AreEqual(new[] { building, intact }, Group(building), "a scene building groups the rest");
            Assert.IsFalse(_em.HasComponent<Prefab>(intact));
        }

        [Test]
        public void PrefabBuilding_DropsItsSectionPrefabsFromItsGroup()
        {
            var building = _em.CreateEntity(typeof(Prefab));
            var intact = Piece(1f, building);
            var broken = Piece(0f, building);
            _em.AddComponent<Prefab>(intact);
            _em.AddComponent<Prefab>(broken);
            _em.AddBuffer<LinkedEntityGroup>(building).AddRange(
                new NativeArray<Entity>([building, intact, broken], Allocator.Temp).Reinterpret<LinkedEntityGroup>());
            _em.AddBuffer<SectionModel>(building).Add(new SectionModel { Intact = intact, Broken = broken });

            Update<ModelCopyBakingSystem>();

            CollectionAssert.AreEqual(new[] { building, intact }, Group(building));
            CollectionAssert.AreEqual(new[] { broken }, Group(broken));
        }

        [Test]
        public void LaterDamageStages_BakeAsPrefabs_AtTheirOwnScale()
        {
            var tree = _em.CreateEntity();
            var whole = Piece(1f, tree);
            var snapped = Piece(2f, tree);
            _em.AddBuffer<DamageStage>(tree).AddRange(new NativeArray<DamageStage>(
            [
                new DamageStage { Below = 1f, Visual = whole }, new DamageStage { Below = 0.3f, Visual = snapped },
            ], Allocator.Temp));

            Update<ModelCopyBakingSystem>();

            Assert.IsFalse(_em.HasComponent<Prefab>(whole));
            Assert.IsTrue(_em.HasComponent<Prefab>(snapped));
            Assert.AreEqual(2f, _em.GetComponentData<LocalTransform>(snapped).Scale);
            CollectionAssert.AreEqual(new[] { tree, whole }, Group(tree));
            Assert.IsTrue(_em.HasBuffer<ModelCopy>(tree));
        }

        [Test]
        public void UnitParts_GetTheUnitsLayer_SceneContentGetsLightTransport_PrefabsKeepTheirs()
        {
            var prefabUnit = _em.CreateEntity(typeof(UnitTag), typeof(Prefab));
            var prefabPart = Rendered(1u);
            _em.AddComponentData(prefabPart, new Parent { Value = prefabUnit });
            var unit = _em.CreateEntity(typeof(UnitTag));
            var part = Placed(1u);
            _em.AddComponentData(part, new Parent { Value = unit });
            var building = _em.CreateEntity(typeof(BuildingTag));
            var wall = Placed(1u);
            _em.AddComponentData(wall, new Parent { Value = building });
            var scenery = Placed(1u);
            var prefabBuilding = _em.CreateEntity(typeof(BuildingTag), typeof(Prefab));
            var prefabWall = Rendered(1u);
            _em.AddComponentData(prefabWall, new Parent { Value = prefabBuilding });
            var spawned = Rendered(1u);

            Update<RenderingLayerBakingSystem>();

            Assert.AreEqual(1u | UnitRenderingLayer.Mask, Layers(prefabPart));
            Assert.AreEqual(1u | UnitRenderingLayer.Mask, Layers(part));
            Assert.AreEqual(1u | LightTransportLayer.Mask, Layers(wall));
            Assert.AreEqual(1u | LightTransportLayer.Mask, Layers(scenery));
            Assert.AreEqual(1u, Layers(prefabWall));
            Assert.AreEqual(1u, Layers(spawned));
        }

        [Test]
        public void VisualPrefab_KeepsTransformsAndMeshes_DropsGameplay()
        {
            var prefab = _em.CreateEntity(typeof(Prefab), typeof(LocalTransform), typeof(LocalToWorld), typeof(Health));
            var part = _em.CreateEntity(typeof(Prefab), typeof(LocalTransform), typeof(Parent), typeof(MaterialMeshInfo));
            _em.SetComponentEnabled<MaterialMeshInfo>(part, false);
            Link(prefab, part);

            var copy = VisualPrefab.Build(_em, prefab);
            var copyPart = _em.GetBuffer<LinkedEntityGroup>(copy)[1].Value;

            Assert.IsTrue(_em.HasComponent<Prefab>(copy));
            Assert.IsTrue(_em.HasComponent<LocalTransform>(copy));
            Assert.IsTrue(_em.HasComponent<LocalToWorld>(copy));
            Assert.IsFalse(_em.HasComponent<Health>(copy));
            Assert.IsTrue(_em.HasComponent<Parent>(copyPart));
            Assert.IsFalse(_em.IsComponentEnabled<MaterialMeshInfo>(copyPart), "a hidden piece stays hidden");
        }

        [Test]
        public void VariantPick_SurvivesReplicationRounding_AndVariesAlongARow()
        {
            var seen = new HashSet<int>();
            for (var i = 0; i < 20; i++)
            {
                var center = new float3(4f + 2f * i, 1.3f, 0.5f);
                var replicated = center + new float3(0.004f, 0.2f, -0.003f);
                var pick = Three.Pick(center);
                Assert.AreEqual(pick, Three.Pick(replicated), $"segment {i}");
                Assert.That(pick, Is.InRange(0, 2));
                seen.Add(pick);
            }

            Assert.Greater(seen.Count, 1, "a wall line doesn't repeat one panel");
        }

        [Test]
        public void VariantSystem_ShowsOnlyThePickedModel_AndMarksItPicked()
        {
            var root = _em.CreateEntity(typeof(HiddenBy), typeof(ModelVariantPicked));
            _em.SetComponentEnabled<ModelVariantPicked>(root, false);
            _em.AddComponentData(root, Three);
            _em.AddComponentData(root, LocalTransform.FromPosition(new float3(6f, 0f, 0.5f)));
            var visuals = new Entity[3];
            for (var i = 0; i < visuals.Length; i++)
            {
                visuals[i] = _em.CreateEntity(typeof(MaterialMeshInfo), typeof(HiddenBy));
            }

            var parts = _em.AddBuffer<ModelVariantPart>(root);
            for (var i = 0; i < visuals.Length; i++)
            {
                parts.Add(new ModelVariantPart { Visual = visuals[i], Variant = i });
            }

            Link(root, visuals);

            Update<ModelVariantSystem>();

            var shown = _em.GetComponentData<ModelVariants>(root).Shown;
            Assert.AreEqual(Three.Pick(new float3(6f, 0f, 0.5f)), shown);
            Assert.IsTrue(_em.IsComponentEnabled<ModelVariantPicked>(root));
            for (var i = 0; i < visuals.Length; i++)
            {
                Assert.AreEqual(i == shown, _em.IsComponentEnabled<MaterialMeshInfo>(visuals[i]), $"variant {i}");
            }
        }

        [Test]
        public void GarrisonPart_ShowsOnlyForItsPassengers()
        {
            const int gunners = 5;
            var bunker = _em.CreateEntity();
            _em.AddBuffer<Cargo>(bunker);
            var gun = _em.CreateEntity(typeof(HiddenBy), typeof(MaterialMeshInfo));
            var writer = new EntityWriter(_em, gun);
            GarrisonPartAuthoring.Add(writer, bunker, new[] { gunners });
            var system = _world.CreateSystem<GarrisonPartSystem>();

            UpdateGarrison(system);
            Assert.IsFalse(Drawn(gun), "empty");

            Board(bunker, 9);
            UpdateGarrison(system);
            Assert.IsFalse(Drawn(gun), "the wrong passengers");

            Board(bunker, gunners);
            UpdateGarrison(system);
            Assert.IsTrue(Drawn(gun), "its passengers aboard");

            _em.GetBuffer<Cargo>(bunker).Clear();
            UpdateGarrison(system);
            Assert.IsFalse(Drawn(gun), "emptied again");
        }

        private void Update<T>() where T : unmanaged, ISystem => _world.CreateSystem<T>().Update(_world.Unmanaged);

        private void UpdateGarrison(SystemHandle system)
        {
            system.Update(_world.Unmanaged);
            _em.CompleteAllTrackedJobs();
        }

        private bool Drawn(Entity entity) => _em.IsComponentEnabled<MaterialMeshInfo>(entity);

        // A model under its parent with the place the transform bake gives it, which is nowhere at scale 0.
        private Entity Piece(float scale, Entity parent)
        {
            if (!_em.HasComponent<LocalToWorld>(parent))
            {
                _em.AddComponentData(parent, new LocalToWorld { Value = float4x4.Translate(Place) });
            }

            var piece = _em.CreateEntity(typeof(MaterialMeshInfo), typeof(HiddenBy));
            var transform = LocalTransform.FromScale(scale);
            var under = _em.GetComponentData<LocalToWorld>(parent).Value;
            _em.AddComponentData(piece, transform);
            _em.AddComponentData(piece, new LocalToWorld { Value = math.mul(under, transform.ToMatrix()) });
            _em.AddComponentData(piece, new Parent { Value = parent });
            return piece;
        }

        private Entity[] Group(Entity root) =>
            _em.GetBuffer<LinkedEntityGroup>(root).Reinterpret<Entity>().AsNativeArray().ToArray();

        private Entity Rendered(uint layers)
        {
            var entity = _em.CreateEntity(typeof(Prefab));
            _em.AddSharedComponent(entity, new RenderFilterSettings { RenderingLayerMask = layers });
            return entity;
        }

        private uint Layers(Entity entity) => _em.GetSharedComponent<RenderFilterSettings>(entity).RenderingLayerMask;

        private Entity Placed(uint layers)
        {
            var entity = Rendered(layers);
            _em.RemoveComponent<Prefab>(entity);
            return entity;
        }

        private void Link(Entity root, params Entity[] parts)
        {
            var group = _em.AddBuffer<LinkedEntityGroup>(root);
            group.Add(root);
            foreach (var part in parts)
            {
                group.Add(part);
            }
        }

        private void Board(Entity container, int typeId)
        {
            var unit = _em.CreateEntity();
            _em.AddComponentData(unit, new EntityInfo { TypeId = typeId });
            _em.GetBuffer<Cargo>(container).Add(new Cargo { Unit = unit, Size = 1 });
        }
    }
}
