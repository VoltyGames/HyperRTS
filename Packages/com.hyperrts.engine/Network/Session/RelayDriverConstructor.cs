using Unity.Entities;
using Unity.NetCode;
using Unity.Networking.Transport.Relay;

namespace HyperRTS.Network.Session
{
    /// <summary>
    /// Netcode drivers for a relay session: the server listens on the relay and over IPC for its own player; a client
    /// joins through the relay, or over IPC to the server in this process when it has no relay data.
    /// </summary>
    public readonly struct RelayDriverConstructor : INetworkStreamDriverConstructor
    {
        private readonly RelayServerData _relay;

        public RelayDriverConstructor(RelayServerData relay) => _relay = relay;

        public void CreateServerDriver(World world, ref NetworkDriverStore driverStore, NetDebug netDebug)
        {
            var relay = _relay;
            DefaultDriverBuilder.RegisterServerDriver(world, ref driverStore, netDebug, ref relay);
        }

        public void CreateClientDriver(World world, ref NetworkDriverStore driverStore, NetDebug netDebug)
        {
            var settings = DefaultDriverBuilder.GetNetworkClientSettings();
            if (!_relay.Endpoint.IsValid)
            {
                DefaultDriverBuilder.RegisterClientIpcDriver(world, ref driverStore, netDebug, settings);
                return;
            }

            var relay = _relay;
            settings.WithRelayParameters(ref relay);
#if UNITY_WEBGL && !UNITY_EDITOR
            DefaultDriverBuilder.RegisterClientWebSocketDriver(world, ref driverStore, netDebug, settings);
#else
            DefaultDriverBuilder.RegisterClientUdpDriver(world, ref driverStore, netDebug, settings);
#endif
        }
    }
}
