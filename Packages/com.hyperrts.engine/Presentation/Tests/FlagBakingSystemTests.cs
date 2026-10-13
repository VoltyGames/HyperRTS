using HyperRTS.Presentation.Flags;
using HyperRTS.Simulation.Buildings;
using NUnit.Framework;
using Unity.Entities;

namespace HyperRTS.Presentation.Tests
{
    public class FlagBakingSystemTests
    {
        [Test]
        public void MountedFlags_GetTheFlagMaterialProperties_PrefabsToo()
        {
            using var world = new World("Flag Baking Tests");
            var em = world.EntityManager;
            var flag = em.CreateEntity(typeof(Prefab));
            var building = em.CreateEntity(typeof(Prefab));
            em.AddComponentData(building, new FlagMount { Flag = flag, Drop = 3f });
            var system = world.CreateSystem<FlagBakingSystem>();

            system.Update(world.Unmanaged);
            system.Update(world.Unmanaged);

            Assert.IsTrue(em.HasComponent<FlagState>(flag));
            Assert.IsTrue(em.HasComponent<FlagColor>(flag));
        }
    }
}
