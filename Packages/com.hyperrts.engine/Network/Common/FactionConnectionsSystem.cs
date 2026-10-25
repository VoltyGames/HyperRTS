using HyperRTS.Core;
using HyperRTS.Network.Players;
using HyperRTS.Simulation.Common;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Netcode;

namespace HyperRTS.Network.Common
{
    /// <summary>Server: regroups the in-game remote connections into <see cref="FactionConnections"/> before the senders.</summary>
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
            // A host's own connection has no stream: its world already holds every event.
            foreach (var (_, connection) in SystemAPI.Query<RefRO<NetworkId>>()
                         .WithAll<NetworkStreamInGame, NetworkStreamConnection>().WithEntityAccess())
            {
                _connections.Add(ViewerFaction(ref state, connection), connection);
            }
        }

        // Observers hold no slot and view as neutral, whose team 0 sees and hears everything.
        private byte ViewerFaction(ref SystemState state, Entity connection)
        {
            if (!SystemAPI.HasComponent<ConnectionPlayer>(connection))
            {
                return Faction.Neutral;
            }

            var player = SystemAPI.GetComponent<ConnectionPlayer>(connection).Player;
            return SystemAPI.GetComponent<Player>(player).Faction;
        }
    }
}
