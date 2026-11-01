using HyperRTS.Network.Players;
using HyperRTS.Simulation.Common;
using NUnit.Framework;
using Unity.Mathematics;
using Unity.Netcode;

namespace HyperRTS.Network.Tests
{
    /// <summary>An observer holds no slot, so it learns it joined from the server's answer, not from a player ghost.</summary>
    public class ObserverJoinTests
    {
        [Test]
        public void ObservingClient_IsToldItJoined_AndOwnsNoPlayer()
        {
            using var session = new HostTestKit(1, 2);
            session.Host.SpawnBuilding(1, float3.zero, new float2(4f, 4f));
            using (var preference = session.Client.Query(typeof(JoinPreference)))
            {
                preference.SetSingleton(new JoinPreference { Observe = true });
            }

            var client = session.Client;
            Assert.IsTrue(session.RunUntil(() => client.Query(typeof(LocalObserver)).CalculateEntityCount() == 1, 20f),
                "the observer hears back");
            Assert.AreEqual(0, client.Query(typeof(LocalPlayer)).CalculateEntityCount(), "an observer has no player");
            Assert.AreEqual(0, session.Host.Get<GhostOwner>(session.Host.Player(2)).NetworkId, "slot 2 stays free");
        }
    }
}
