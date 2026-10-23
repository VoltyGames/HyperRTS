using System.Collections.Generic;
using HyperRTS.Presentation.Rendering;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;

namespace HyperRTS.Presentation.Tests
{
    /// <summary>The position-hashed model variant pick every client must agree on, and the system that shows it.</summary>
    public class ModelVariantsTests
    {
        private static readonly ModelVariants Three = new() { Count = 3, Shown = -1 };

        [Test]
        public void Pick_SurvivesReplicationRounding()
        {
            for (var i = 0; i < 20; i++)
            {
                var center = new float3(4f + 2f * i, 1.3f, 0.5f);
                var replicated = center + new float3(0.004f, 0.2f, -0.003f);
                Assert.AreEqual(Three.Pick(center), Three.Pick(replicated), $"segment {i}");
            }
        }

        [Test]
        public void Pick_StaysInRange_AndVariesAlongARow()
        {
            var seen = new HashSet<int>();
            for (var i = 0; i < 12; i++)
            {
                var pick = Three.Pick(new float3(4f + 2f * i, 0f, 0.5f));
                Assert.That(pick, Is.InRange(0, 2));
                seen.Add(pick);
            }

            Assert.Greater(seen.Count, 1, "a wall line doesn't repeat one panel");
        }
    
        [Test]
        public void System_ShowsOnlyThePickedModel_AndMarksItPicked()
        {
            using var kit = new PresentationWorldKit("Model Variant Tests");
            var em = kit.EntityManager;
            var system = kit.World.CreateSystem<ModelVariantSystem>();
            var root = em.CreateEntity(typeof(HiddenBy), typeof(ModelVariantPicked));
            em.SetComponentEnabled<ModelVariantPicked>(root, false);
            em.AddComponentData(root, Three);
            em.AddComponentData(root, LocalTransform.FromPosition(new float3(6f, 0f, 0.5f)));
            var visuals = new Entity[3];
            var group = new NativeList<LinkedEntityGroup>(Allocator.Temp) { root };
            for (var i = 0; i < visuals.Length; i++)
            {
                visuals[i] = em.CreateEntity(typeof(MaterialMeshInfo), typeof(HiddenBy));
                group.Add(visuals[i]);
            }

            var parts = em.AddBuffer<ModelVariantPart>(root);
            for (var i = 0; i < visuals.Length; i++)
            {
                parts.Add(new ModelVariantPart { Visual = visuals[i], Variant = i });
            }

            em.AddBuffer<LinkedEntityGroup>(root).AddRange(group.AsArray());

            system.Update(kit.World.Unmanaged);

            var shown = em.GetComponentData<ModelVariants>(root).Shown;
            Assert.AreEqual(Three.Pick(new float3(6f, 0f, 0.5f)), shown);
            Assert.IsTrue(em.IsComponentEnabled<ModelVariantPicked>(root));
            for (var i = 0; i < visuals.Length; i++)
            {
                Assert.AreEqual(i == shown, em.IsComponentEnabled<MaterialMeshInfo>(visuals[i]), $"variant {i}");
            }
        }
    }
}
