using System.Text.RegularExpressions;
using HyperRTS.Network.Session;
using NUnit.Framework;
using Unity.Entities;
using Unity.Netcode;
using Unity.Networking.Transport.Relay;
using UnityEngine;
using UnityEngine.TestTools;

namespace HyperRTS.Simulation.Tests
{
    /// <summary>Relay drivers, the lobby-facing connection status and rejoin guards, without opening sockets.</summary>
    public class NetworkSessionTests
    {
        // Session statics outlive Play mode in the editor; start each test as a fresh player would.
        [SetUp]
        public void ResetSession() => NetworkSession.ResetStatics();

        [TestCase(ConnectionState.State.Connecting, NetworkStatus.Connecting)]
        [TestCase(ConnectionState.State.Handshake, NetworkStatus.Connecting)]
        [TestCase(ConnectionState.State.Approval, NetworkStatus.Connecting)]
        [TestCase(ConnectionState.State.Connected, NetworkStatus.Connected)]
        [TestCase(ConnectionState.State.Disconnected, NetworkStatus.Disconnected)]
        public void ConnectionStateMapsToStatus(ConnectionState.State state, NetworkStatus expected) =>
            Assert.AreEqual(expected, NetworkStatusSystem.ToStatus(state));

        [TestCase(WorldFlags.GameServer | WorldFlags.GameClient, TestName = "RelayHostListensOnTheRelayOnly")]
        [TestCase(WorldFlags.GameClient, TestName = "RelayClientJoinsThroughTheRelay")]
        public void DriverStore_HasOneRelaySocket(WorldFlags flags)
        {
            var store = Build(flags, FakeRelay());
            Assert.AreEqual(1, store.DriversCount);
            Assert.AreEqual(TransportType.Socket, store.GetDriverType(store.FirstDriver));
            store.Dispose();
        }

        [Test]
        public void Token_IsNeverZero() => Assert.AreNotEqual(0UL, NetworkSession.Token);

        [Test]
        public void Rejoin_WithoutADirectJoin_ChangesNothing()
        {
            LogAssert.Expect(LogType.Error, new Regex("Nothing to rejoin"));
            Assert.IsFalse(NetworkSession.CanRejoin);
            Assert.IsFalse(NetworkSession.Rejoin(SessionScene.Keep));
        }

        private static NetworkDriverStore Build(WorldFlags flags, RelayServerData relay)
        {
            ClientServerBootstrap.AutoConnectPort = 0;
            using var world = flags == WorldFlags.GameClient
                ? ClientServerBootstrap.CreateClientWorld("Relay Test")
                : ClientServerBootstrap.CreateSingleWorldHost("Relay Test");
            var constructor = new RelayDriverConstructor(relay);
            var store = new NetworkDriverStore();
            var netDebug = new NetDebug { LogLevel = NetDebug.LogLevelType.Error };
            if (world.IsServer())
            {
                constructor.CreateServerDriver(world, ref store, netDebug);
            }
            else
            {
                constructor.CreateClientDriver(world, ref store, netDebug);
            }

            return store;
        }

        // Transport rejects an all-zero allocation id.
        private static RelayServerData FakeRelay()
        {
            var allocationId = new byte[16];
            allocationId[0] = 1;
            return new RelayServerData("127.0.0.1", 7777, allocationId, new byte[255], new byte[255], new byte[64], false);
        }
    }
}
