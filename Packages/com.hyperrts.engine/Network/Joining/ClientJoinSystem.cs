using HyperRTS.Network.Common;
using HyperRTS.Simulation.Common.Players;
using Unity.Collections;
using Unity.Entities;
using Unity.Netcode;

namespace HyperRTS.Network.Joining
{
    /// <summary>
    /// Asks for a slot once the match has loaded, and takes the granted player ghost as the <see cref="LocalPlayer"/>.
    /// On a host, <see cref="ServerJoinSystem"/> seats the player instead.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct ClientJoinSystem : ISystem
    {
        private RpcInbox<ObserverAccepted> _observing;

        public void OnCreate(ref SystemState state) => _observing = new RpcInbox<ObserverAccepted>(ref state);

        public void OnUpdate(ref SystemState state)
        {
            RequestJoin(ref state);
            ReceiveObserver(ref state);
            TagLocalPlayer(ref state);
        }

        private void RequestJoin(ref SystemState state)
        {
            // A host's own connection has no stream: ServerJoinSystem seats it directly.
            var connections = SystemAPI.QueryBuilder().WithAll<NetworkId, LocalConnection, NetworkStreamConnection>()
                .WithNone<NetworkStreamInGame>().Build();
            var matchLoaded = !SystemAPI.QueryBuilder().WithAll<Player, Prefab>()
                .WithOptions(EntityQueryOptions.IncludePrefab).Build().IsEmpty;
            if (connections.IsEmpty || !matchLoaded)
            {
                return;
            }

            SystemAPI.TryGetSingleton(out JoinPreference preference);
            SystemAPI.TryGetSingleton(out JoinToken token);
            var entityManager = state.EntityManager;
            foreach (var connection in connections.ToEntityArray(Allocator.Temp))
            {
                entityManager.AddComponent<NetworkStreamInGame>(connection);
                Rpc.Send(entityManager, connection, JoinRequest.From(preference, token.Value));
            }
        }

        private void ReceiveObserver(ref SystemState state)
        {
            if (_observing.IsEmpty)
            {
                return;
            }

            _observing.Take(state.EntityManager);
            if (!SystemAPI.HasSingleton<LocalObserver>())
            {
                state.EntityManager.CreateSingleton<LocalObserver>();
            }
        }

        private void TagLocalPlayer(ref SystemState state)
        {
            var owned = SystemAPI.QueryBuilder().WithAll<Player, GhostOwnerIsLocal>().Build();
            if (SystemAPI.HasSingleton<LocalPlayer>() || owned.IsEmpty)
            {
                return;
            }

            state.EntityManager.AddComponent<LocalPlayer>(owned.ToEntityArray(Allocator.Temp)[0]);
        }
    }
}
