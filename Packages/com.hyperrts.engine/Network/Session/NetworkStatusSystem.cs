using Unity.Entities;
using Unity.NetCode;

namespace HyperRTS.Network.Session
{
    /// <summary>Publishes this client's Netcode connection events to <see cref="NetworkSession.Status"/>.</summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct NetworkStatusSystem : ISystem
    {
        public void OnCreate(ref SystemState state) => state.RequireForUpdate<NetworkStreamDriver>();

        public void OnUpdate(ref SystemState state)
        {
            foreach (var evt in SystemAPI.GetSingleton<NetworkStreamDriver>().ConnectionEventsForTick)
            {
                NetworkSession.SetStatus(ToStatus(evt.State), evt.DisconnectReason);
            }
        }

        public static NetworkStatus ToStatus(ConnectionState.State state) => state switch
        {
            ConnectionState.State.Connected => NetworkStatus.Connected,
            ConnectionState.State.Disconnected => NetworkStatus.Disconnected,
            _ => NetworkStatus.Connecting,
        };
    }
}
