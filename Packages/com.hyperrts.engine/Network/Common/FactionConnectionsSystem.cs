using HyperRTS.Core;
using HyperRTS.Network.Players;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Netcode;

namespace HyperRTS.Network.Common
{
    /// <summary>Server: regroups the in-game connections into <see cref="FactionConnections"/> before the senders.</summary>
    [BurstCompile]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(LifecycleSystemGroup), OrderLast = true)]
    public partial struct FactionConnectionsSystem : ISystem
    {
        private FactionConnections _connections;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _connections = FactionConnections.Create(Allocator.Persistent);
            state.EntityManager.CreateSingleton(_connections);
        }

        public void OnDestroy(ref SystemState state) => _connections.Dispose();

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _connections.Clear();
            foreach (var (_, connection) in SystemAPI.Query<RefRO<NetworkId>>().WithAll<NetworkStreamInGame>()
                         .WithEntityAccess())
            {
                _connections.Add(ConnectionPlayers.Faction(ref state, connection), connection);
            }
        }
    }
}
