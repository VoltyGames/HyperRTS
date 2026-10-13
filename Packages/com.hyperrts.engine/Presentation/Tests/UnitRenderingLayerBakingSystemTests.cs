using HyperRTS.Presentation.Rendering;
using HyperRTS.Simulation.Common;
using NUnit.Framework;
using Unity.Entities;
using Unity.Entities.Graphics;
using Unity.Transforms;

namespace HyperRTS.Presentation.Tests
{
    public class UnitRenderingLayerBakingSystemTests
    {
        [Test]
        public void UnitParts_GetTheUnitsLayer_OtherRenderersKeepTheirs()
        {
            using var world = new World("Unit Layer Baking Tests");
            var em = world.EntityManager;
            var unit = em.CreateEntity(typeof(UnitTag), typeof(Prefab));
            var part = Rendered(em, 1u);
            em.AddComponentData(part, new Parent { Value = unit });
            var scenery = Rendered(em, 1u);
            var system = world.CreateSystem<UnitRenderingLayerBakingSystem>();

            system.Update(world.Unmanaged);

            Assert.AreEqual(1u | UnitRenderingLayer.Mask, em.GetSharedComponent<RenderFilterSettings>(part).RenderingLayerMask);
            Assert.AreEqual(1u, em.GetSharedComponent<RenderFilterSettings>(scenery).RenderingLayerMask);
        }

        private static Entity Rendered(EntityManager em, uint layers)
        {
            var entity = em.CreateEntity(typeof(Prefab));
            em.AddSharedComponent(entity, new RenderFilterSettings { RenderingLayerMask = layers });
            return entity;
        }
    }
}
