using System.Reflection;
using System.Text.RegularExpressions;
using HyperRTS.Network.Session;
using NUnit.Framework;
using Unity.Entities;
using Unity.NetCode;
using Unity.Networking.Transport.Relay;
using UnityEngine;
using UnityEngine.TestTools;

namespace HyperRTS.Simulation.Tests
{
    /// <summary>Relay driver choice, the lobby-facing connection status and rejoin guards, without opening sockets.</summary>
    public class NetworkSessionTests
    {
        // Session statics outlive Play mode in the editor; start each test as a fresh player would.
        [SetUp]
        public void ResetSession() =>
            typeof(NetworkSession).GetMethod("ResetStatics", BindingFlags.NonPublic | BindingFlags.Static)!
                .Invoke(null, null);

        [TestCase(ConnectionState.State.Connecting, NetworkStatus.Connecting)]
        [TestCase(ConnectionState.State.Handshake, NetworkStatus.Connecting)]
        [TestCase(ConnectionState.State.Approval, NetworkStatus.Connecting)]
        [TestCase(ConnectionState.State.Connected, NetworkStatus.Connected)]
        [TestCase(ConnectionState.State.Disconnected, NetworkStatus.Disconnected)]
        public void ConnectionStateMapsToStatus(ConnectionState.State state, NetworkStatus expected) =>
            Assert.AreEqual(expected, NetworkStatusSystem.ToStatus(state));

        [TestCase(WorldFlags.GameServer, true, new[] { TransportType.IPC, TransportType.Socket },
            TestName = "RelayServerListensOverIPCAndRelay")]
        [TestCase(WorldFlags.GameClient, true, new[] { TransportType.Socket },
            TestName = "RelayClientJoinsThroughTheRelay")]
        [TestCase(WorldFlags.GameClient, false, new[] { TransportType.IPC },
            TestName = "HostClientWithoutRelayDataUsesIPC")]
        public void DriverStore_UsesTheExpectedTransports(WorldFlags flags, bool relay, TransportType[] expected)
        {
            var store = Build(flags, relay ? FakeRelay() : default);
            Assert.AreEqual(expected.Length, store.DriversCount);
            for (var i = 0; i < expected.Length; i++)
            {
                Assert.AreEqual(expected[i], store.GetDriverType(store.FirstDriver + i));
            }

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
            using var world = new World("Relay Test", flags);
            var constructor = new RelayDriverConstructor(relay);
            var store = new NetworkDriverStore();
            var netDebug = new NetDebug { LogLevel = NetDebug.LogLevelType.Error };
            if (flags == WorldFlags.GameServer)
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
