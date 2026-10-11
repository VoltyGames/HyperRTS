using HyperRTS.Network.Replication;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Production;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Tests
{
    /// <summary>Clients fill in prefab references that snapshots carry only as type ids.</summary>
    public class ReplicationTests
    {
        [Test]
        public void PrefabReferences_ResolveFromTypeIds()
        {
            using var world = new TestWorld();
            world.CreateMatch(1);
            var prefab = world.MakePrefab(world.SpawnUnit(0, new float3(50f, 0f, 50f), name: "Scout"));
            world.Tick();
            var holder = world.EntityManager.CreateEntity();
            world.EntityManager.AddBuffer<ProductionQueueItem>(holder)
                .Add(new ProductionQueueItem { TypeId = EntityInfo.TypeIdFromName("Scout") });

            var registry = world.EntityManager.CreateEntityQuery(typeof(PrefabRegistry)).GetSingleton<PrefabRegistry>();
            var holders = world.EntityManager.CreateEntityQuery(typeof(ProductionQueueItem));
            PrefabReferences.Resolve<ProductionQueueItem>(world.EntityManager, holders, registry);

            Assert.AreEqual(prefab, world.EntityManager.GetBuffer<ProductionQueueItem>(holder)[0].Prefab);
        }
    }
}
