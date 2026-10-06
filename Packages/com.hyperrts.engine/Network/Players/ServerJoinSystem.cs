using HyperRTS.Simulation.Match;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;

namespace HyperRTS.Network.Players
{
    /// <summary>
    /// Binds joining connections to free human slots (observers when none is left) and frees the slot of a dropped
    /// connection, so its owner can reconnect while the units wait.
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

            var entityManager = state.EntityManager;
            foreach (var request in requests.ToEntityArray(Allocator.Temp))
            {
                var connection = entityManager.GetComponentData<ReceiveRpcCommandRequest>(request).SourceConnection;
                var wanted = entityManager.GetComponentData<JoinRequest>(request).Faction;
                var networkId = entityManager.GetComponentData<NetworkId>(connection).Value;
                var slot = Claim(ref state, wanted, networkId);
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
                entityManager.DestroyEntity(request);
            }
        }

        /// <summary>The wanted slot if it is free, else the first free human slot, else none (observer).</summary>
        private Entity Claim(ref SystemState state, byte wanted, int networkId)
        {
            var slot = Entity.Null;
            foreach (var (player, connection, entity) in SystemAPI.Query<RefRO<Player>, RefRO<PlayerConnection>>()
                         .WithNone<AIPlayer>().WithEntityAccess())
            {
                if (connection.ValueRO.NetworkId != 0)
                {
                    continue;
                }

                if (player.ValueRO.Faction == wanted)
                {
                    slot = entity;
                    break;
                }

                if (slot == Entity.Null)
                {
                    slot = entity;
                }
            }

            if (slot != Entity.Null)
            {
                SystemAPI.SetComponent(slot, new PlayerConnection { NetworkId = networkId });
            }

            return slot;
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
