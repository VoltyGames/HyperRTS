using System.Linq;
using System.Reflection;
using HyperRTS.Presentation.Rendering.LitTeam;
using HyperRTS.Presentation.Rendering.Parts;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Destruction;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;

namespace HyperRTS.Presentation.Tests.Destruction
{
    /// <summary>
    /// A tree's models stepping from whole to stripped to snapped as its health drops; the later ones are copied in
    /// from their prefabs while they show.
    /// </summary>
    public class DamageStageTests
    {
        private static readonly float[] Thresholds = { 1f, 0.6f, 0.3f };

        private PresentationWorldKit _kit;
        private EntityManager _em;
        private DamageStageSystem _stages;

        [SetUp]
        public void SetUp()
        {
            _kit = new PresentationWorldKit("Damage Stage Tests");
            _em = _kit.EntityManager;
            _stages = _kit.World.CreateSystemManaged<DamageStageSystem>();
        }

        [TearDown]
        public void TearDown() => _kit.Dispose();

        [TestCase(260f, 0)]
        [TestCase(150f, 1)]
        [TestCase(70f, 2)]
        public void Tree_ShowsTheModelForItsHealth(float health, int expected)
        {
            var tree = Tree(health, out var models);

            _stages.Update();

            AssertOnlyShown(models, expected);
            Assert.AreEqual(expected, _em.GetComponentData<ShownDamageStage>(tree).Index);
        }

        [Test]
        public void Tree_StepsDownAsHealthDrops_AndBackUpWhenHealed()
        {
            var tree = Tree(260f, out var models);
            _stages.Update();

            SetHealth(tree, 100f);
            _stages.Update();
            AssertOnlyShown(models, 1);

            SetHealth(tree, 20f);
            _stages.Update();
            AssertOnlyShown(models, 2);

            SetHealth(tree, 260f);
            _stages.Update();
            AssertOnlyShown(models, 0);
            Assert.AreEqual(0, _em.GetBuffer<ModelCopy>(tree).Length, "the stripped and snapped copies are gone");
        }

        [Test]
        public void StagesWithoutModels_StillStep_AndLeaveTheModelAlone()
        {
            // Like a garrison building whose stages only play effects.
            var building = _em.CreateEntity();
            _em.AddComponentData(building, new LocalToWorld { Value = float4x4.identity });
            _em.AddComponentData(building, new Health { Current = 260f, Max = 260f });
            _em.AddComponentData(building, new ShownDamageStage { Index = -1 });
            _em.AddBuffer<DamageStage>(building).AddRange(new NativeArray<DamageStage>(new[]
            {
                new DamageStage { Below = 1f }, new DamageStage { Below = 0.5f },
            }, Allocator.Temp));
            _em.AddBuffer<ModelCopy>(building);
            _stages.Update();

            SetHealth(building, 100f);
            _stages.Update();

            Assert.AreEqual(1, _em.GetComponentData<ShownDamageStage>(building).Index);
            Assert.AreEqual(0, _em.GetBuffer<ModelCopy>(building).Length, "no model to copy in");
        }

        [Test]
        public void Copy_TakesEveryPaintOfTheTree()
        {
            var tree = Tree(260f, out var models);
            var painted = PaintedTypes();
            foreach (var type in painted)
            {
                _em.AddComponent(models[0], type);
            }

            var paint = new TeamColor { Value = new float4(0.2f, 0.4f, 0.6f, 1f) };
            _em.SetComponentData(models[0], paint);
            _stages.Update();

            SetHealth(tree, 100f);
            _stages.Update();

            var copy = Copy(tree, models[1]);
            Assert.AreEqual(paint.Value, _em.GetComponentData<TeamColor>(copy).Value);
            foreach (var type in painted)
            {
                Assert.IsTrue(_em.HasComponent(copy, type), $"{type} is painted on a root once, so a copy needs it from ModelCopies");
            }
        }

        [Test]
        public void Instance_CopiesUnderItsOwnPartsAndPlace()
        {
            var prefab = _em.CreateEntity(typeof(HiddenBy), typeof(Prefab));
            _em.AddComponentData(prefab, new LocalToWorld { Value = float4x4.identity });
            _em.AddComponentData(prefab, new Health { Current = 260f, Max = 260f });
            _em.AddComponentData(prefab, new ShownDamageStage { Index = -1 });
            var crown = _em.CreateEntity(typeof(Prefab));
            _em.AddComponentData(crown, new Parent { Value = prefab });
            _em.AddComponentData(crown, new LocalToWorld { Value = float4x4.Translate(new float3(0f, 3f, 0f)) });
            var whole = _em.CreateEntity(typeof(MaterialMeshInfo), typeof(HiddenBy), typeof(Prefab));
            _em.AddComponentData(whole, new Parent { Value = crown });
            var stripped = StageModel(crown);
            _em.AddComponentData(stripped, new LocalToWorld { Value = float4x4.Translate(new float3(0f, 3f, 0f)) });
            _em.AddBuffer<DamageStage>(prefab).AddRange(new NativeArray<DamageStage>(new[]
            {
                new DamageStage { Below = 1f, Visual = whole }, new DamageStage { Below = 0.6f, Visual = stripped },
            }, Allocator.Temp));
            _em.AddBuffer<LinkedEntityGroup>(prefab).AddRange(
                new NativeArray<Entity>(new[] { prefab, crown, whole }, Allocator.Temp).Reinterpret<LinkedEntityGroup>());
            _em.AddBuffer<ModelCopy>(prefab);

            var tree = _em.Instantiate(prefab);
            _em.SetComponentData(tree, new LocalToWorld { Value = float4x4.Translate(new float3(50f, 0f, 0f)) });
            var ownCrown = _em.GetBuffer<LinkedEntityGroup>(tree)[1].Value;
            _em.SetComponentData(ownCrown, new LocalToWorld { Value = float4x4.Translate(new float3(50f, 3f, 0f)) });
            _stages.Update();
            SetHealth(tree, 100f);
            _stages.Update();

            var copy = Copy(tree, stripped);
            Assert.AreEqual(ownCrown, _em.GetComponentData<Parent>(copy).Value, "under the instance's own crown");
            Assert.AreEqual(new float3(50f, 3f, 0f), _em.GetComponentData<LocalToWorld>(copy).Position,
                "drawn where the tree stands before the transforms run");
        }

        private Entity Tree(float health, out Entity[] models)
        {
            // Like a map tree, which has no hide reasons of its own.
            var tree = _em.CreateEntity();
            _em.AddComponentData(tree, new LocalToWorld { Value = float4x4.identity });
            _em.AddComponentData(tree, new Health { Current = health, Max = 260f });
            _em.AddComponentData(tree, new ShownDamageStage { Index = -1 });
            models = new Entity[Thresholds.Length];
            models[0] = _em.CreateEntity(typeof(MaterialMeshInfo), typeof(HiddenBy));
            _em.AddComponentData(models[0], new Parent { Value = tree });
            for (var i = 1; i < Thresholds.Length; i++)
            {
                models[i] = StageModel(tree);
            }

            var stages = new NativeList<DamageStage>(Allocator.Temp);
            for (var i = 0; i < Thresholds.Length; i++)
            {
                stages.Add(new DamageStage { Below = Thresholds[i], Visual = models[i] });
            }

            _em.AddBuffer<DamageStage>(tree).AddRange(stages.AsArray());
            _em.AddBuffer<LinkedEntityGroup>(tree).AddRange(
                new NativeArray<Entity>(new[] { tree, models[0] }, Allocator.Temp).Reinterpret<LinkedEntityGroup>());
            _em.AddBuffer<ModelCopy>(tree);
            return tree;
        }

        // A later stage, baked as a prefab of its own under the given part.
        private Entity StageModel(Entity parent)
        {
            var model = _em.CreateEntity(typeof(MaterialMeshInfo), typeof(HiddenBy), typeof(Prefab));
            _em.AddComponentData(model, new Parent { Value = parent });
            _em.AddBuffer<LinkedEntityGroup>(model).Add(model);
            return model;
        }

        // Every value a MeshPainter puts on a root's meshes.
        private static ComponentType[] PaintedTypes()
        {
            const BindingFlags fields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            var painted = typeof(MeshPainter<>).Assembly.GetTypes()
                .SelectMany(type => type.GetFields(fields))
                .Select(field => field.FieldType)
                .Where(type => type.IsGenericType && type.GetGenericTypeDefinition() == typeof(MeshPainter<>))
                .Select(type => type.GetGenericArguments()[0])
                .Distinct()
                .Select(type => new ComponentType(type))
                .ToArray();
            Assert.That(painted.Length, Is.GreaterThan(2), "the painters were found");
            return painted;
        }

        private Entity Copy(Entity tree, Entity model) => ModelCopies.Find(_em, tree, model);

        private void SetHealth(Entity tree, float current)
        {
            var health = _em.GetComponentData<Health>(tree);
            health.Current = current;
            _em.SetComponentData(tree, health);
        }

        private void AssertOnlyShown(Entity[] models, int shown)
        {
            var tree = _em.GetComponentData<Parent>(models[0]).Value;
            Assert.AreEqual(shown == 0, _em.IsComponentEnabled<MaterialMeshInfo>(models[0]), "model 0");
            for (var i = 1; i < models.Length; i++)
            {
                var copy = Copy(tree, models[i]);
                Assert.AreEqual(i == shown, copy != Entity.Null && _em.IsComponentEnabled<MaterialMeshInfo>(copy), $"model {i}");
            }
        }
    }
}
