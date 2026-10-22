using HyperRTS.Simulation.AI;
using HyperRTS.Simulation.Common;
using Unity.Collections;
using Unity.Entities;
using Unity.Netcode;

namespace HyperRTS.Network.Players
{
    /// <summary>
    /// Binds joining connections to human slots (observers when none is left or they asked to watch) and frees the
    /// slot of a dropped connection, keeping it for its owner's token so only the owner can reconnect to it.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct ServerJoinSystem : ISystem
    {
        public void OnCreate(ref SystemState state) => state.RequireForUpdate<NetworkStreamDriver>();

        public void OnUpdate(ref SystemState state)
        {
            // Network ids are reused, so drops are cleared before this tick's joins can claim one again.
            ReleaseDropped(ref state);
            var requests = SystemAPI.QueryBuilder().WithAll<JoinRequest, ReceiveRpcCommandRequest>().Build();
            var players = SystemAPI.QueryBuilder().WithAll<Player, PlayerConnection>().Build();
            if (requests.IsEmpty || players.IsEmpty)
            {
                return;
            }

            foreach (var request in requests.ToEntityArray(Allocator.Temp))
            {
                Join(ref state, request);
                state.EntityManager.DestroyEntity(request);
            }
        }

        /// <summary>Binds the sender to a slot; repeats from a connection already in game can't claim more.</summary>
        private void Join(ref SystemState state, Entity request)
        {
            var entityManager = state.EntityManager;
            var connection = entityManager.GetComponentData<ReceiveRpcCommandRequest>(request).SourceConnection;
            if (!entityManager.Exists(connection) || entityManager.HasComponent<NetworkStreamInGame>(connection))
            {
                return;
            }

            var wanted = entityManager.GetComponentData<JoinRequest>(request);
            var networkId = entityManager.GetComponentData<NetworkId>(connection).Value;
            var slot = wanted.Observe ? Entity.Null : Claim(ref state, wanted, networkId);
            byte faction = 0;
            if (slot != Entity.Null)
            {
                entityManager.AddComponentData(connection, new ConnectionPlayer { Player = slot });
                faction = entityManager.GetComponentData<Player>(slot).Faction;
            }

            entityManager.AddComponent<NetworkStreamInGame>(connection);
            var reply = entityManager.CreateEntity();
            entityManager.AddComponentData(reply, new JoinAccepted { Faction = faction });
            entityManager.AddComponentData(reply, new SendRpcCommandRequest { TargetConnection = connection });
        }

        /// <summary>Binds the slot <see cref="SlotClaim"/> picks; Entity.Null makes the sender an observer.</summary>
        private Entity Claim(ref SystemState state, JoinRequest wanted, int networkId)
        {
            var entities = new NativeList<Entity>(Allocator.Temp);
            var slots = new NativeList<SlotClaim.Slot>(Allocator.Temp);
            foreach (var (player, connection, entity) in SystemAPI.Query<RefRO<Player>, RefRO<PlayerConnection>>()
                         .WithNone<AIPlayer>().WithEntityAccess())
            {
                entities.Add(entity);
                slots.Add(new SlotClaim.Slot
                {
                    Faction = player.ValueRO.Faction,
                    NetworkId = connection.ValueRO.NetworkId,
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
            SystemAPI.SetComponent(slot, new PlayerConnection { NetworkId = networkId, Token = wanted.Token });
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

                foreach (var connection in SystemAPI.Query<RefRW<PlayerConnection>>())
                {
                    if (connection.ValueRO.NetworkId == evt.Id.Value)
                    {
                        connection.ValueRW.NetworkId = 0;
                    }
                }
            }
        }
    }
}
