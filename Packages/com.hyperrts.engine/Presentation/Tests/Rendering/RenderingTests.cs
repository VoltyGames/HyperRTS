using System.Collections.Generic;
using HyperRTS.Presentation.Destruction;
using HyperRTS.Presentation.Rendering;
using HyperRTS.Simulation.Combat;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Transport;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Entities.Graphics;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;

namespace HyperRTS.Presentation.Tests.Rendering
{
    /// <summary>What draws: baking for hiding and unit layers, visual prefabs, model variants and garrison parts.</summary>
    public class RenderingTests
    {
        private static readonly ModelVariants Three = new() { Count = 3, Shown = -1 };

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
        public void HiddenByBaking_MarksMeshesAndRoots_OtherEntitiesNot()
        {
            var mesh = _em.CreateEntity(typeof(MaterialMeshInfo), typeof(Prefab));
            var root = _em.CreateEntity(typeof(EntityInfo), typeof(Prefab));
            var other = _em.CreateEntity(typeof(Prefab));

            Update<HiddenByBakingSystem>();

            Assert.IsTrue(_em.HasComponent<HiddenBy>(mesh));
            Assert.IsTrue(_em.HasComponent<HiddenBy>(root));
            Assert.IsFalse(_em.HasComponent<HiddenBy>(other));
        }

        [Test]
        public void BrokenPiece_BakesFullSizeAndHidden_WithItsChildMeshes()
        {
            var intact = Piece(1f);
            var broken = Piece(0f);
            var shard = _em.CreateEntity(typeof(MaterialMeshInfo), typeof(HiddenBy), typeof(Prefab));
            _em.AddComponentData(shard, new Parent { Value = broken });
            var building = _em.CreateEntity(typeof(Prefab));
            _em.AddBuffer<BreakablePart>(building).Add(new BreakablePart { Intact = intact, Broken = broken });

            Update<BreakablePartsBakingSystem>();

            Assert.AreEqual(1f, _em.GetComponentData<LocalTransform>(broken).Scale);
            Assert.IsFalse(_em.IsComponentEnabled<MaterialMeshInfo>(broken));
            Assert.IsFalse(_em.IsComponentEnabled<MaterialMeshInfo>(shard));
            Assert.IsTrue(_em.GetComponentData<HiddenBy>(shard).Has(HideReason.Part));
            Assert.IsTrue(_em.IsComponentEnabled<MaterialMeshInfo>(intact));
        }

        [Test]
        public void UnitParts_GetTheUnitsLayer_OtherRenderersKeepTheirs()
        {
            var unit = _em.CreateEntity(typeof(UnitTag), typeof(Prefab));
            var part = Rendered(1u);
            _em.AddComponentData(part, new Parent { Value = unit });
            var scenery = Rendered(1u);

            Update<UnitRenderingLayerBakingSystem>();

            Assert.AreEqual(1u | UnitRenderingLayer.Mask, _em.GetSharedComponent<RenderFilterSettings>(part).RenderingLayerMask);
            Assert.AreEqual(1u, _em.GetSharedComponent<RenderFilterSettings>(scenery).RenderingLayerMask);
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
            var writer = new EntityManagerWriter(_em, gun);
            GarrisonPartAuthoring.Add(ref writer, bunker, new[] { gunners });
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

        private Entity Piece(float scale)
        {
            var piece = _em.CreateEntity(typeof(MaterialMeshInfo), typeof(HiddenBy), typeof(Prefab));
            _em.AddComponentData(piece, LocalTransform.FromScale(scale));
            return piece;
        }

        private Entity Rendered(uint layers)
        {
            var entity = _em.CreateEntity(typeof(Prefab));
            _em.AddSharedComponent(entity, new RenderFilterSettings { RenderingLayerMask = layers });
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
