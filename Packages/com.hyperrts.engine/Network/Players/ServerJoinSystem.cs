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
        public void OnUpdate(ref SystemState state)
        {
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
                var faction = Claim(ref state, wanted, entityManager.GetComponentData<NetworkId>(connection).Value);

                entityManager.AddComponent<NetworkStreamInGame>(connection);
                var reply = entityManager.CreateEntity();
                entityManager.AddComponentData(reply, new JoinAccepted { Faction = faction });
                entityManager.AddComponentData(reply, new SendRpcCommandRequest { TargetConnection = connection });
                entityManager.DestroyEntity(request);
            }
        }

        /// <summary>The wanted slot if it is free, else the first free human slot, else 0 (observer).</summary>
        private byte Claim(ref SystemState state, byte wanted, int networkId)
        {
            var slot = Entity.Null;
            foreach (var (player, connection, entity) in SystemAPI.Query<RefRO<Player>, RefRO<PlayerConnection>>()
                         .WithNone<AIPlayer>().WithEntityAccess())
            {
                var free = connection.ValueRO.NetworkId == 0;
                if (free && (slot == Entity.Null || player.ValueRO.Faction == wanted))
                {
                    slot = entity;
                }
            }

            if (slot == Entity.Null)
            {
                return 0;
            }

            SystemAPI.SetComponent(slot, new PlayerConnection { NetworkId = networkId });
            return SystemAPI.GetComponent<Player>(slot).Faction;
        }

        private void ReleaseDropped(ref SystemState state)
        {
            var live = new NativeHashSet<int>(8, Allocator.Temp);
            foreach (var id in SystemAPI.Query<RefRO<NetworkId>>())
            {
                live.Add(id.ValueRO.Value);
            }

            foreach (var connection in SystemAPI.Query<RefRW<PlayerConnection>>())
            {
                if (connection.ValueRO.NetworkId != 0 && !live.Contains(connection.ValueRO.NetworkId))
                {
                    connection.ValueRW.NetworkId = 0;
                }
            }
        }
    }
}
