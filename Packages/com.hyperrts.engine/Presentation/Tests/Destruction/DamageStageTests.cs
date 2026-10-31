using HyperRTS.Presentation.Destruction;
using HyperRTS.Presentation.Rendering.Visibility;
using HyperRTS.Simulation.Combat;
using HyperRTS.Simulation.Common;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Rendering;
using Unity.Transforms;

namespace HyperRTS.Presentation.Tests.Destruction
{
    /// <summary>A tree's models stepping from whole to stripped to snapped as its health drops.</summary>
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
        }

        private Entity Tree(float health, out Entity[] models)
        {
            var tree = _em.CreateEntity(typeof(HiddenBy), typeof(LocalToWorld));
            _em.AddComponentData(tree, new Health { Current = health, Max = 260f });
            _em.AddComponentData(tree, new ShownDamageStage { Index = -1 });
            var group = new NativeList<LinkedEntityGroup>(Allocator.Temp) { tree };
            var stages = new NativeList<DamageStage>(Allocator.Temp);
            models = new Entity[Thresholds.Length];
            for (var i = 0; i < Thresholds.Length; i++)
            {
                models[i] = _em.CreateEntity(typeof(MaterialMeshInfo), typeof(HiddenBy));
                stages.Add(new DamageStage { Below = Thresholds[i], Visual = models[i] });
                group.Add(models[i]);
            }

            _em.AddBuffer<DamageStage>(tree).AddRange(stages.AsArray());
            _em.AddBuffer<LinkedEntityGroup>(tree).AddRange(group.AsArray());
            return tree;
        }

        private void SetHealth(Entity tree, float current)
        {
            var health = _em.GetComponentData<Health>(tree);
            health.Current = current;
            _em.SetComponentData(tree, health);
        }

        private void AssertOnlyShown(Entity[] models, int shown)
        {
            for (var i = 0; i < models.Length; i++)
            {
                Assert.AreEqual(i == shown, _em.IsComponentEnabled<MaterialMeshInfo>(models[i]), $"model {i}");
            }
        }
    }
}
