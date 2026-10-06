using HyperRTS.Network.Session;
using NUnit.Framework;
using Unity.Entities;
using Unity.NetCode;
using Unity.Networking.Transport.Relay;

namespace HyperRTS.Simulation.Tests
{
    /// <summary>Relay driver choice and the lobby-facing connection status, without opening sockets.</summary>
    public class NetworkSessionTests
    {
        [TestCase(ConnectionState.State.Connecting, NetworkStatus.Connecting)]
        [TestCase(ConnectionState.State.Handshake, NetworkStatus.Connecting)]
        [TestCase(ConnectionState.State.Approval, NetworkStatus.Connecting)]
        [TestCase(ConnectionState.State.Connected, NetworkStatus.Connected)]
        [TestCase(ConnectionState.State.Disconnected, NetworkStatus.Disconnected)]
        public void ConnectionStateMapsToStatus(ConnectionState.State state, NetworkStatus expected) =>
            Assert.AreEqual(expected, NetworkStatusSystem.ToStatus(state));

        [Test]
        public void RelayServerListensOverIPCAndRelay()
        {
            var store = Build(WorldFlags.GameServer, FakeRelay());
            Assert.AreEqual(2, store.DriversCount);
            Assert.AreEqual(TransportType.IPC, store.GetDriverType(store.FirstDriver));
            Assert.AreEqual(TransportType.Socket, store.GetDriverType(store.FirstDriver + 1));
            store.Dispose();
        }

        [Test]
        public void RelayClientJoinsThroughTheRelay()
        {
            var store = Build(WorldFlags.GameClient, FakeRelay());
            Assert.AreEqual(1, store.DriversCount);
            Assert.AreEqual(TransportType.Socket, store.GetDriverType(store.FirstDriver));
            store.Dispose();
        }

        [Test]
        public void HostClientWithoutRelayDataUsesIPC()
        {
            var store = Build(WorldFlags.GameClient, default);
            Assert.AreEqual(1, store.DriversCount);
            Assert.AreEqual(TransportType.IPC, store.GetDriverType(store.FirstDriver));
            store.Dispose();
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
