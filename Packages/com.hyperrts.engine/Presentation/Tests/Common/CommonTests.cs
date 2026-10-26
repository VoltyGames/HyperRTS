using System;
using System.Linq;
using System.Reflection;
using HyperRTS.Presentation.Common;
using HyperRTS.Presentation.Effects;
using HyperRTS.Presentation.Impacts;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Cues;
using NUnit.Framework;
using Unity.Entities;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HyperRTS.Presentation.Tests.Common
{
    /// <summary>Shared presentation plumbing: the behaviour base, the per-type data catalogue and faction relations.</summary>
    public class CommonTests
    {
        private const int TypeId = 42;
        private const BindingFlags Declared =
            BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        private PresentationWorldKit _kit;
        private EntityManager _em;
        private PrefabRegistry _registry;
        private TypeCatalog<EffectSet> _catalog;

        [SetUp]
        public void SetUp()
        {
            _kit = new PresentationWorldKit("Common Tests");
            _em = _kit.EntityManager;
            _registry = _kit.AddRegistry();
            _catalog = new TypeCatalog<EffectSet>(_em.CreateEntityQuery(new EntityQueryDesc
            {
                All = new[] { ComponentType.ReadOnly<EntityInfo>() },
                Any = new[] { ComponentType.ReadOnly<EntityEffect>(), ComponentType.ReadOnly<EntityImpactEffects>() },
                Options = EntityQueryOptions.IncludePrefab,
            }), EffectSet.Read);
        }

        [TearDown]
        public void TearDown() => _kit.Dispose();

        [Test]
        public void EveryBehaviour_DerivesFromPresentationBehaviour()
        {
            var strays = Behaviours().Where(type => !typeof(PresentationBehaviour).IsAssignableFrom(type));
            CollectionAssert.IsEmpty(strays.Select(type => type.FullName));
        }

        // A subclass's own Awake or OnDestroy would replace the base's and skip the graphics check.
        [Test]
        public void NoBehaviour_DeclaresItsOwnAwakeOrOnDestroy()
        {
            var strays = Behaviours().Where(type =>
                type.GetMethod("Awake", Declared) != null || type.GetMethod("OnDestroy", Declared) != null);
            CollectionAssert.IsEmpty(strays.Select(type => type.FullName));
        }

        [Test]
        public void Catalog_ForgetsAMiss_OnceAnEntityWithDataAppears()
        {
            Assert.IsNull(_catalog.Find(_em, _registry, TypeId));

            var entity = CreateTyped(false);
            _em.AddBuffer<EntityEffect>(entity).Add(new EntityEffect { Slot = CueSlot.Fire });

            var set = _catalog.Find(_em, _registry, TypeId);
            Assert.IsNotNull(set);
            Assert.IsTrue(set.TryGet(CueSlot.Fire, 0, out _));
            Assert.IsFalse(set.TryGet(CueSlot.Death, 0, out _));
            Assert.IsNull(set.Impacts);
        }

        [Test]
        public void Catalog_PrefersThePrefabOverInstances()
        {
            var prefab = CreateTyped(true);
            _em.AddBuffer<EntityEffect>(prefab).Add(new EntityEffect { Slot = CueSlot.Death });
            _kit.RefreshRegistry();

            var set = _catalog.Find(_em, _registry, TypeId);

            Assert.IsTrue(set.TryGet(CueSlot.Death, 0, out _));
        }

        [Test]
        public void Catalog_PrefabWithoutData_IsAFinalMiss()
        {
            CreateTyped(true);
            _kit.RefreshRegistry();
            Assert.IsNull(_catalog.Find(_em, _registry, TypeId));

            var entity = CreateTyped(false);
            _em.AddBuffer<EntityEffect>(entity).Add(new EntityEffect { Slot = CueSlot.Fire });

            Assert.IsNull(_catalog.Find(_em, _registry, TypeId), "the prefab speaks for its instances");
        }

        [Test]
        public void Catalog_ReadsImpactMarksFromAnInstance()
        {
            var mark = ScriptableObject.CreateInstance<ImpactMark>();
            try
            {
                var marks = new TypeCatalog<ImpactMark[]>(_em.CreateEntityQuery(new EntityQueryDesc
                {
                    All = new[] { ComponentType.ReadOnly<EntityInfo>(), ComponentType.ReadOnly<EntityImpactMark>() },
                    Options = EntityQueryOptions.IncludePrefab,
                }), ImpactMarkSystem.ReadMarks);
                var entity = CreateTyped(false);
                _em.AddBuffer<EntityImpactMark>(entity).Add(new EntityImpactMark { Mark = mark });

                var found = marks.Find(_em, _registry, TypeId);

                Assert.AreEqual(1, found.Length);
                Assert.AreSame(mark, found[0]);
            }
            finally
            {
                Object.DestroyImmediate(mark);
            }
        }

        [Test]
        public void GetRelation_ClassifiesOwnAllyEnemyNeutral()
        {
            var relations = new FactionRelations();
            relations.Teams.Add(0);
            relations.Teams.Add(1);
            relations.Teams.Add(1);
            relations.Teams.Add(2);

            Assert.AreEqual(Relation.Own, relations.GetRelation(1, 1));
            Assert.AreEqual(Relation.Ally, relations.GetRelation(1, 2));
            Assert.AreEqual(Relation.Enemy, relations.GetRelation(1, 3));
            Assert.AreEqual(Relation.Neutral, relations.GetRelation(1, Faction.Neutral));
        }

        // Authoring is baked away and never runs, so it keeps the AuthoringBehaviour base instead.
        private static Type[] Behaviours() =>
            new[] { typeof(GraphicsSupport).Assembly, Assembly.Load("HyperRTS.Input") }
            .SelectMany(assembly => assembly.GetTypes())
            .Where(type => typeof(MonoBehaviour).IsAssignableFrom(type) && type != typeof(PresentationBehaviour))
            .Where(type => !typeof(AuthoringBehaviour).IsAssignableFrom(type))
            .ToArray();

        private Entity CreateTyped(bool prefab)
        {
            var entity = prefab
                ? _em.CreateEntity(typeof(EntityInfo), typeof(Prefab))
                : _em.CreateEntity(typeof(EntityInfo));
            _em.SetComponentData(entity, new EntityInfo { TypeId = TypeId });
            return entity;
        }
    }
}
