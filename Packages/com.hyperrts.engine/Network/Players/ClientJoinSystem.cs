using HyperRTS.Network.Session;
using HyperRTS.Simulation.Match;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;

namespace HyperRTS.Network.Players
{
    /// <summary>
    /// Asks for a slot once the match has loaded, then tags the player ghost of the granted slot as the
    /// <see cref="LocalPlayer"/> that input and HUD read.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct ClientJoinSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            RequestJoin(ref state);
            ReceiveAccepted(ref state);
            TagLocalPlayer(ref state);
        }

        private void RequestJoin(ref SystemState state)
        {
            var connections = SystemAPI.QueryBuilder().WithAll<NetworkId>().WithNone<NetworkStreamInGame>().Build();
            var matchLoaded = !SystemAPI.QueryBuilder().WithAll<Player, Prefab>()
                .WithOptions(EntityQueryOptions.IncludePrefab).Build().IsEmpty;
            if (connections.IsEmpty || !matchLoaded)
            {
                return;
            }

            var entityManager = state.EntityManager;
            foreach (var connection in connections.ToEntityArray(Allocator.Temp))
            {
                entityManager.AddComponent<NetworkStreamInGame>(connection);
                var request = entityManager.CreateEntity();
                entityManager.AddComponentData(request, new JoinRequest { Faction = NetworkSession.PreferredFaction });
                entityManager.AddComponent<SendRpcCommandRequest>(request);
            }
        }

        private void ReceiveAccepted(ref SystemState state)
        {
            var accepted = SystemAPI.QueryBuilder().WithAll<JoinAccepted, ReceiveRpcCommandRequest>().Build();
            foreach (var reply in accepted.ToEntityArray(Allocator.Temp))
            {
                var faction = state.EntityManager.GetComponentData<JoinAccepted>(reply).Faction;
                NetworkSession.PreferredFaction = faction;
                if (!SystemAPI.TryGetSingletonRW<LocalFaction>(out var local))
                {
                    state.EntityManager.CreateSingleton(new LocalFaction { Value = faction });
                }
                else
                {
                    local.ValueRW.Value = faction;
                }

                state.EntityManager.DestroyEntity(reply);
            }
        }

        private void TagLocalPlayer(ref SystemState state)
        {
            if (SystemAPI.HasSingleton<LocalPlayer>() || !SystemAPI.TryGetSingleton<LocalFaction>(out var local))
            {
                return;
            }

            var mine = Entity.Null;
            foreach (var (player, entity) in SystemAPI.Query<RefRO<Player>>().WithEntityAccess())
            {
                if (player.ValueRO.Faction == local.Value)
                {
                    mine = entity;
                }
            }

            if (mine != Entity.Null)
            {
                state.EntityManager.AddComponent<LocalPlayer>(mine);
            }
        }
    }
}
