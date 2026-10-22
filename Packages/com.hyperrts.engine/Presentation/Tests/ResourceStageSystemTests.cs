using System;
using HyperRTS.Presentation.Destruction;
using HyperRTS.Presentation.Rendering;
using HyperRTS.Simulation.Resources;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Rendering;
using Unity.Transforms;

namespace HyperRTS.Presentation.Tests
{
    /// <summary>The resource stage system on a four-stage pile: it shrinks as it empties and grows back as it regrows.</summary>
    public class ResourceStageSystemTests
    {
        private static readonly float[] Thresholds = { 1f, 2f / 3f, 1f / 3f, 0.12f };

        private PresentationWorldKit _kit;
        private EntityManager _em;
        private SystemHandle _system;

        [SetUp]
        public void SetUp()
        {
            _kit = new PresentationWorldKit("Resource Stage Tests");
            _em = _kit.EntityManager;
            _system = _kit.World.CreateSystem<ResourceStageSystem>();
        }

        [TearDown]
        public void TearDown() => _kit.Dispose();

        [TestCase(300, 0)]
        [TestCase(201, 0)]
        [TestCase(200, 1)]
        [TestCase(100, 2)]
        [TestCase(36, 3)]
        [TestCase(1, 3)]
        public void FirstFrame_ShowsTheStageForTheRemainingShare(int amount, int expected)
        {
            var pile = Pile(amount, out var visuals);

            _system.Update(_kit.World.Unmanaged);

            Assert.AreEqual(expected, _em.GetComponentData<ShownResourceStage>(pile).Index);
            AssertOnlyShown(visuals, expected);
        }

        [Test]
        public void Gathering_StepsDown_AndRegrowth_StepsBackUp()
        {
            var pile = Pile(300, out var visuals);
            _system.Update(_kit.World.Unmanaged);

            SetAmount(pile, 90);
            _system.Update(_kit.World.Unmanaged);
            AssertOnlyShown(visuals, 2);

            SetAmount(pile, 250);
            _system.Update(_kit.World.Unmanaged);
            AssertOnlyShown(visuals, 0);
        }

        /// <summary>A pile whose stage models each hold a child mesh, linked as an instantiated prefab is.</summary>
        private Entity Pile(int amount, out Entity[] visuals)
        {
            var pile = _em.CreateEntity(typeof(HiddenBy));
            _em.AddComponentData(pile, new ResourceNode { Amount = amount, MaxAmount = 300 });
            _em.AddComponentData(pile, new ShownResourceStage { Index = -1 });
            var stages = new ResourceStage[Thresholds.Length];
            var group = new NativeList<LinkedEntityGroup>(Allocator.Temp) { pile };
            visuals = new Entity[Thresholds.Length];
            for (var i = 0; i < Thresholds.Length; i++)
            {
                visuals[i] = _em.CreateEntity(typeof(MaterialMeshInfo), typeof(HiddenBy));
                var mesh = _em.CreateEntity(typeof(MaterialMeshInfo), typeof(HiddenBy));
                _em.AddComponentData(mesh, new Parent { Value = visuals[i] });
                stages[i] = new ResourceStage { Below = Thresholds[i], Visual = visuals[i] };
                group.Add(visuals[i]);
                group.Add(mesh);
            }

            _em.AddBuffer<ResourceStage>(pile).AddRange(new NativeArray<ResourceStage>(stages, Allocator.Temp));
            _em.AddBuffer<LinkedEntityGroup>(pile).AddRange(group.AsArray());
            return pile;
        }

        private void SetAmount(Entity pile, int amount)
        {
            var node = _em.GetComponentData<ResourceNode>(pile);
            node.Amount = amount;
            _em.SetComponentData(pile, node);
        }

        private void AssertOnlyShown(Entity[] visuals, int shown)
        {
            for (var i = 0; i < visuals.Length; i++)
            {
                Assert.AreEqual(i == shown, _em.IsComponentEnabled<MaterialMeshInfo>(visuals[i]), $"stage {i}");
            }

            using var query = _em.CreateEntityQuery(typeof(Parent));
            using var meshes = query.ToEntityArray(Allocator.Temp);
            foreach (var mesh in meshes)
            {
                var stage = Array.IndexOf(visuals, _em.GetComponentData<Parent>(mesh).Value);
                Assert.AreEqual(stage == shown, _em.IsComponentEnabled<MaterialMeshInfo>(mesh), $"stage {stage} child");
            }
        }
    }
}
