using HyperRTS.Network.Replication;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Production;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;

namespace HyperRTS.Simulation.Tests
{
    /// <summary>Clients fill in prefab references that snapshots carry only as type ids, and predict only in game.</summary>
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

        [Test]
        public void Prediction_RunsOnlyWhileInGame()
        {
            using var world = new World("In-Game Prediction Test", WorldFlags.GameClient);
            var prediction = world.CreateSystemManaged<PredictedSimulationSystemGroup>();
            var inGamePrediction = world.CreateSystem<InGamePredictionSystem>();

            inGamePrediction.Update(world.Unmanaged);
            Assert.IsFalse(prediction.Enabled, "No connection in game yet.");

            var connection = world.EntityManager.CreateEntity(typeof(NetworkStreamInGame));
            inGamePrediction.Update(world.Unmanaged);
            Assert.IsTrue(prediction.Enabled, "In game.");

            world.EntityManager.DestroyEntity(connection);
            inGamePrediction.Update(world.Unmanaged);
            Assert.IsFalse(prediction.Enabled, "The connection dropped.");
        }
    }
}
