using HyperRTS.Presentation.Rendering;
using HyperRTS.Simulation.Common;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Rendering;
using Unity.Transforms;

namespace HyperRTS.Presentation.Tests
{
    /// <summary>A visual prefab keeps what draws a building and drops its gameplay.</summary>
    public class VisualPrefabTests
    {
        [Test]
        public void Build_KeepsTransformsAndMeshes_DropsGameplay()
        {
            using var world = new World("Visual Prefab Tests");
            var em = world.EntityManager;
            var prefab = em.CreateEntity(typeof(Prefab), typeof(LocalTransform), typeof(LocalToWorld), typeof(Health));
            var part = em.CreateEntity(typeof(Prefab), typeof(LocalTransform), typeof(Parent), typeof(MaterialMeshInfo));
            em.SetComponentEnabled<MaterialMeshInfo>(part, false);
            em.AddBuffer<LinkedEntityGroup>(prefab).AddRange(new NativeArray<LinkedEntityGroup>(
                new LinkedEntityGroup[] { prefab, part }, Allocator.Temp));

            var copy = VisualPrefab.Build(em, prefab);
            var copyPart = em.GetBuffer<LinkedEntityGroup>(copy)[1].Value;

            Assert.IsTrue(em.HasComponent<Prefab>(copy));
            Assert.IsTrue(em.HasComponent<LocalTransform>(copy));
            Assert.IsTrue(em.HasComponent<LocalToWorld>(copy));
            Assert.IsFalse(em.HasComponent<Health>(copy));
            Assert.IsTrue(em.HasComponent<Parent>(copyPart));
            Assert.IsFalse(em.IsComponentEnabled<MaterialMeshInfo>(copyPart), "a hidden piece stays hidden");
        }
    }
}
