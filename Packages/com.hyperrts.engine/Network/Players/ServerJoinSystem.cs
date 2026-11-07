using HyperRTS.Network.Common;
using HyperRTS.Simulation.AI;
using HyperRTS.Simulation.Common.Players;
using Unity.Collections;
using Unity.Entities;
using Unity.Netcode;

namespace HyperRTS.Network.Players
{
    /// <summary>
    /// Binds joining connections to human slots (observers when none is left or they asked to watch) and frees the
    /// slot of a dropped connection, keeping it for its owner's token so only the owner can reconnect to it. A seat
    /// sets the player ghost's <see cref="GhostOwner"/>; an observer gets <see cref="ObserverAccepted"/>. A host's
    /// own <see cref="LocalConnection"/> joins here too.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct ServerJoinSystem : ISystem
    {
        private RpcInbox<JoinRequest> _requests;

        public void OnCreate(ref SystemState state)
        {
            _requests = new RpcInbox<JoinRequest>(ref state);
            state.RequireForUpdate<NetworkStreamDriver>();
        }

        public void OnUpdate(ref SystemState state)
        {
            // Network ids are reused, so drops are cleared before this tick's joins can claim one again.
            ReleaseDropped(ref state);
            var players = SystemAPI.QueryBuilder().WithAll<Player, PlayerConnection>().Build();
            if (players.IsEmpty)
            {
                return;
            }

            JoinHost(ref state);
            var requests = _requests.Take(state.EntityManager, out var senders);
            for (var i = 0; i < requests.Length; i++)
            {
                Join(ref state, senders[i], requests[i]);
            }
        }

        /// <summary>A host's own player joins without a request, asking for what its session prefers.</summary>
        private void JoinHost(ref SystemState state)
        {
            var seating = SystemAPI.QueryBuilder().WithAll<LocalConnection>().WithNone<NetworkStreamInGame>().Build();
            if (seating.IsEmpty)
            {
                return;
            }

            var wanted = JoinRequest.From(SystemAPI.GetSingleton<JoinPreference>(),
                SystemAPI.GetSingleton<JoinToken>().Value);
            Join(ref state, seating.GetSingletonEntity(), wanted);
        }

        /// <summary>Binds the connection to a slot; repeats from a connection already in game can't claim more.</summary>
        private void Join(ref SystemState state, Entity connection, JoinRequest wanted)
        {
            var entityManager = state.EntityManager;
            if (!entityManager.Exists(connection) || entityManager.HasComponent<NetworkStreamInGame>(connection))
            {
                return;
            }

            var networkId = entityManager.GetComponentData<NetworkId>(connection).Value;
            var slot = wanted.Observe ? Entity.Null : Claim(ref state, wanted, networkId);
            if (slot != Entity.Null)
            {
                entityManager.AddComponentData(connection, new ConnectionPlayer { Player = slot });
            }
            else
            {
                Rpc.Send(entityManager, connection, new ObserverAccepted());
            }

            entityManager.AddComponent<NetworkStreamInGame>(connection);
        }

        /// <summary>Binds the slot <see cref="SlotClaim"/> picks; Entity.Null makes the sender an observer.</summary>
        private Entity Claim(ref SystemState state, JoinRequest wanted, int networkId)
        {
            var entities = new NativeList<Entity>(Allocator.Temp);
            var slots = new NativeList<SlotClaim.Slot>(Allocator.Temp);
            foreach (var (player, connection, owner, entity) in SystemAPI
                         .Query<RefRO<Player>, RefRO<PlayerConnection>, RefRO<GhostOwner>>().WithNone<AIPlayer>()
                         .WithEntityAccess())
            {
                entities.Add(entity);
                slots.Add(new SlotClaim.Slot
                {
                    Faction = player.ValueRO.Faction,
                    NetworkId = owner.ValueRO.NetworkId,
                    Token = connection.ValueRO.Token,
                });
            }

            var index = SlotClaim.Choose(slots.AsArray().AsReadOnlySpan(), wanted.Faction, wanted.Token);
            if (index < 0)
            {
                return Entity.Null;
            }

            var slot = entities[index];
            DisconnectStale(ref state, slots[index].NetworkId);
            SystemAPI.SetComponent(slot, new GhostOwner { NetworkId = networkId });
            SystemAPI.SetComponent(slot, new PlayerConnection { Token = wanted.Token });

            // A selection the old connection sent means nothing to the new client.
            SystemAPI.GetBuffer<PlayerSelection>(slot).Clear();
            return slot;
        }

        /// <summary>Its owner came back on a new connection before the old one timed out: the old one goes.</summary>
        private void DisconnectStale(ref SystemState state, int networkId)
        {
            if (networkId == 0)
            {
                return;
            }

            var stale = Entity.Null;
            foreach (var (id, connection) in SystemAPI.Query<RefRO<NetworkId>>().WithEntityAccess())
            {
                if (id.ValueRO.Value == networkId)
                {
                    stale = connection;
                    break;
                }
            }

            // Changed after the loop: structural changes aren't allowed while iterating.
            if (stale == Entity.Null)
            {
                return;
            }

            state.EntityManager.RemoveComponent<ConnectionPlayer>(stale);
            state.EntityManager.AddComponentData(stale,
                new NetworkStreamRequestDisconnect { Reason = NetworkStreamDisconnectReason.ConnectionClose });
        }

        private void ReleaseDropped(ref SystemState state)
        {
            foreach (var evt in SystemAPI.GetSingleton<NetworkStreamDriver>().ConnectionEventsForTick)
            {
                if (evt.State != ConnectionState.State.Disconnected)
                {
                    continue;
                }

                foreach (var owner in SystemAPI.Query<RefRW<GhostOwner>>().WithAll<Player, PlayerConnection>())
                {
                    if (owner.ValueRO.NetworkId == evt.Id.Value)
                    {
                        owner.ValueRW.NetworkId = 0;
                    }
                }
            }
        }
    }
}
