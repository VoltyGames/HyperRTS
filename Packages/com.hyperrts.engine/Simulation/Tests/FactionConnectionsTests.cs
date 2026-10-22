using HyperRTS.Network.Common;
using HyperRTS.Network.Cues;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Netcode;

namespace HyperRTS.Simulation.Tests
{
    /// <summary>Connections group by viewer faction, and each faction's batches reach all its connections.</summary>
    public class FactionConnectionsTests
    {
        [Test]
        public void Factions_AreListedOnce_AndBatchesReachEveryConnectionOfTheFaction()
        {
            using var world = new TestWorld();
            var entities = world.EntityManager;
            var allies = new[] { entities.CreateEntity(), entities.CreateEntity() };
            var observer = entities.CreateEntity();
            var viewers = FactionConnections.Create(Allocator.Temp);
            viewers.Add(1, allies[0]);
            viewers.Add(0, observer);
            viewers.Add(1, allies[1]);
            using var batches = new NativeList<CueRpc>(Allocator.Temp) { new CueRpc(), new CueRpc() };
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            viewers.Send(ecb, 1, batches);
            ecb.Playback(entities);
            ecb.Dispose();

            CollectionAssert.AreEqual(new byte[] { 1, 0 }, viewers.Factions.AsArray().ToArray());
            using var query = entities.CreateEntityQuery(typeof(CueRpc), typeof(SendRpcCommandRequest));
            var requests = query.ToComponentDataArray<SendRpcCommandRequest>(Allocator.Temp);
            Assert.AreEqual(4, requests.Length, "two batches to each of the two allies");
            foreach (var request in requests)
            {
                Assert.AreNotEqual(observer, request.TargetConnection, "the observer views as another faction");
            }

            viewers.Dispose();
        }
    }
}
