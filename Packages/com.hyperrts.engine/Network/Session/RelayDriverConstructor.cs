using Unity.Netcode;
using Unity.Networking.Transport.Relay;

namespace HyperRTS.Network.Session;

/// <summary>Netcode drivers for a relay session: the host listens on the relay and a client joins through it.</summary>
public readonly struct RelayDriverConstructor(RelayServerData relay) : INetworkStreamDriverConstructor
{
    public void CreateServerDriver(NetcodeWorld world, ref NetworkDriverStore driverStore, NetDebug netDebug)
    {
        var data = relay;
        var settings = DefaultDriverBuilder.GetNetworkServerSettings();
        settings.WithRelayParameters(ref data);
        DefaultDriverBuilder.RegisterServerUdpDriver(world, ref driverStore, netDebug, settings);
    }

    public void CreateClientDriver(NetcodeWorld world, ref NetworkDriverStore driverStore, NetDebug netDebug)
    {
        var data = relay;
        var settings = DefaultDriverBuilder.GetNetworkClientSettings();
        settings.WithRelayParameters(ref data);
#if UNITY_WEBGL && !UNITY_EDITOR
        DefaultDriverBuilder.RegisterClientWebSocketDriver(world, ref driverStore, netDebug, settings);
#else
        DefaultDriverBuilder.RegisterClientUdpDriver(world, ref driverStore, netDebug, settings);
#endif
    }
}
