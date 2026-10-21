using HyperRTS.Presentation.Common;
using HyperRTS.Presentation.Effects;
using HyperRTS.Presentation.Impacts;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Cues;
using NUnit.Framework;
using Unity.Entities;
using UnityEngine;

namespace HyperRTS.Presentation.Tests
{
    public class TypeCatalogTests
    {
        private const int TypeId = 42;

        private PresentationWorldKit _kit;
        private EntityManager _entityManager;
        private PrefabRegistry _registry;
        private TypeCatalog<EffectSet> _catalog;

        [SetUp]
        public void SetUp()
        {
            _kit = new PresentationWorldKit("Type Catalog Test");
            _entityManager = _kit.EntityManager;
            _registry = _kit.AddRegistry();
            _catalog = new TypeCatalog<EffectSet>(_entityManager.CreateEntityQuery(new EntityQueryDesc
            {
                All = new[] { ComponentType.ReadOnly<EntityInfo>() },
                Any = new[] { ComponentType.ReadOnly<EntityEffect>(), ComponentType.ReadOnly<EntityImpactEffects>() },
                Options = EntityQueryOptions.IncludePrefab,
            }), EffectSet.Read);
        }

        [TearDown]
        public void TearDown() => _kit.Dispose();

        [Test]
        public void Miss_IsForgotten_OnceAnEntityWithDataAppears()
        {
            Assert.IsNull(_catalog.Find(_entityManager, _registry, TypeId));

            var entity = CreateTyped(false);
            _entityManager.AddBuffer<EntityEffect>(entity).Add(new EntityEffect { Slot = CueSlot.Fire });

            var set = _catalog.Find(_entityManager, _registry, TypeId);
            Assert.IsNotNull(set);
            Assert.IsTrue(set.TryGet(CueSlot.Fire, 0, out _));
            Assert.IsFalse(set.TryGet(CueSlot.Death, 0, out _));
            Assert.IsNull(set.Impacts);
        }

        [Test]
        public void Prefab_IsPreferredOverInstances()
        {
            var prefab = CreateTyped(true);
            _entityManager.AddBuffer<EntityEffect>(prefab).Add(new EntityEffect { Slot = CueSlot.Death });
            _kit.RefreshRegistry();

            var set = _catalog.Find(_entityManager, _registry, TypeId);

            Assert.IsTrue(set.TryGet(CueSlot.Death, 0, out _));
        }

        [Test]
        public void PrefabWithoutData_IsAFinalMiss()
        {
            CreateTyped(true);
            _kit.RefreshRegistry();
            Assert.IsNull(_catalog.Find(_entityManager, _registry, TypeId));

            var entity = CreateTyped(false);
            _entityManager.AddBuffer<EntityEffect>(entity).Add(new EntityEffect { Slot = CueSlot.Fire });

            Assert.IsNull(_catalog.Find(_entityManager, _registry, TypeId), "the prefab speaks for its instances");
        }

        [Test]
        public void ImpactMarks_AreReadFromAnInstance()
        {
            var mark = ScriptableObject.CreateInstance<ImpactMark>();
            try
            {
                var marks = new TypeCatalog<ImpactMark[]>(_entityManager.CreateEntityQuery(new EntityQueryDesc
                {
                    All = new[] { ComponentType.ReadOnly<EntityInfo>(), ComponentType.ReadOnly<EntityImpactMark>() },
                    Options = EntityQueryOptions.IncludePrefab,
                }), ImpactMarkSystem.ReadMarks);
                var entity = CreateTyped(false);
                _entityManager.AddBuffer<EntityImpactMark>(entity).Add(new EntityImpactMark { Mark = mark });

                var found = marks.Find(_entityManager, _registry, TypeId);

                Assert.AreEqual(1, found.Length);
                Assert.AreSame(mark, found[0]);
            }
            finally
            {
                Object.DestroyImmediate(mark);
            }
        }

        private Entity CreateTyped(bool prefab)
        {
            var entity = prefab
                ? _entityManager.CreateEntity(typeof(EntityInfo), typeof(Prefab))
                : _entityManager.CreateEntity(typeof(EntityInfo));
            _entityManager.SetComponentData(entity, new EntityInfo { TypeId = TypeId });
            return entity;
        }
    }
}
