using HyperRTS.Presentation.Fog;
using HyperRTS.Presentation.Rendering;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;

namespace HyperRTS.Presentation.Tests
{
    /// <summary>Render hierarchy collection reaches every part of large structures.</summary>
    public class RenderHierarchyTests
    {
        private const int Parts = 100;

        private World _world;
        private EntityManager _manager;

        [SetUp]
        public void SetUp()
        {
            _world = new World("RenderHierarchyTests");
            _manager = _world.EntityManager;
        }

        [TearDown]
        public void TearDown() => _world.Dispose();

        [Test]
        public void Collect_LinkedGroupPastSixtyThreeParts_ListsEveryPart()
        {
            var root = _manager.CreateEntity();
            var group = _manager.AddBuffer<LinkedEntityGroup>(root);
            group.Add(root);
            for (var i = 1; i < Parts; i++)
            {
                group = _manager.GetBuffer<LinkedEntityGroup>(root);
                group.Add(_manager.CreateEntity());
            }

            Assert.AreEqual(Parts, CollectedCount(root));
        }

        [Test]
        public void Collect_TransformChildrenPastSixtyThreeParts_ListsEveryPart()
        {
            var root = _manager.CreateEntity();
            _manager.AddBuffer<Child>(root);
            for (var i = 1; i < Parts; i++)
            {
                var part = _manager.CreateEntity();
                _manager.GetBuffer<Child>(root).Add(new Child { Value = part });
            }

            Assert.AreEqual(Parts, CollectedCount(root));
        }

        private int CollectedCount(Entity root)
        {
            // Any system's state builds the lookups; the fog visibility system collects hierarchies itself.
            var world = _world.Unmanaged;
            ref var state = ref world.ResolveSystemStateRef(_world.CreateSystem<FogVisibilitySystem>());
            using var targets = new NativeList<Entity>(Allocator.Temp);
            RenderHierarchy.Collect(root, state.GetBufferLookup<LinkedEntityGroup>(true),
                state.GetBufferLookup<Child>(true), targets);
            return targets.Length;
        }
    }
}
