using HyperRTS.Presentation.Effects;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Effects;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;

namespace HyperRTS.Presentation.Tests
{
    public class EffectCatalogTests
    {
        private const int TypeId = 42;

        private World _world;
        private EntityManager _entityManager;
        private PrefabRegistry _registry;
        private EffectCatalog _catalog;

        [SetUp]
        public void SetUp()
        {
            _world = new World("Effect Catalog Test");
            _entityManager = _world.EntityManager;
            _registry = new PrefabRegistry { ByTypeId = new NativeHashMap<int, Entity>(1, Allocator.Persistent) };
            _catalog = new EffectCatalog(_entityManager.CreateEntityQuery(new EntityQueryDesc
            {
                All = new[] { ComponentType.ReadOnly<EntityInfo>() },
                Any = new[] { ComponentType.ReadOnly<EntityEffect>(), ComponentType.ReadOnly<EntityImpactEffects>() },
                Options = EntityQueryOptions.IncludePrefab,
            }));
        }

        [TearDown]
        public void TearDown()
        {
            _registry.ByTypeId.Dispose();
            _world.Dispose();
        }

        [Test]
        public void Miss_IsForgotten_OnceAnEntityWithEffectsAppears()
        {
            Assert.IsNull(_catalog.Find(_entityManager, _registry, TypeId));

            var entity = _entityManager.CreateEntity(typeof(EntityInfo));
            _entityManager.SetComponentData(entity, new EntityInfo { TypeId = TypeId });
            _entityManager.AddBuffer<EntityEffect>(entity).Add(new EntityEffect { Slot = EffectSlot.Fire });

            var set = _catalog.Find(_entityManager, _registry, TypeId);
            Assert.IsNotNull(set);
            Assert.IsTrue(set.TryGet(EffectSlot.Fire, out _));
            Assert.IsFalse(set.TryGet(EffectSlot.Death, out _));
            Assert.IsNull(set.Impacts);
        }

        [Test]
        public void Prefab_IsPreferredOverInstances()
        {
            var prefab = _entityManager.CreateEntity(typeof(EntityInfo), typeof(Prefab));
            _entityManager.SetComponentData(prefab, new EntityInfo { TypeId = TypeId });
            _entityManager.AddBuffer<EntityEffect>(prefab).Add(new EntityEffect { Slot = EffectSlot.Death });
            _registry.ByTypeId.Add(TypeId, prefab);

            var set = _catalog.Find(_entityManager, _registry, TypeId);

            Assert.IsTrue(set.TryGet(EffectSlot.Death, out _));
        }

        [Test]
        public void PrefabWithoutEffects_IsAFinalMiss()
        {
            var prefab = _entityManager.CreateEntity(typeof(EntityInfo), typeof(Prefab));
            _entityManager.SetComponentData(prefab, new EntityInfo { TypeId = TypeId });
            _registry.ByTypeId.Add(TypeId, prefab);
            Assert.IsNull(_catalog.Find(_entityManager, _registry, TypeId));

            var entity = _entityManager.CreateEntity(typeof(EntityInfo));
            _entityManager.SetComponentData(entity, new EntityInfo { TypeId = TypeId });
            _entityManager.AddBuffer<EntityEffect>(entity).Add(new EntityEffect { Slot = EffectSlot.Fire });

            Assert.IsNull(_catalog.Find(_entityManager, _registry, TypeId), "the prefab speaks for its instances");
        }
    }
}
