using HyperRTS.Network.Players;
using HyperRTS.Simulation.Match;
using HyperRTS.Simulation.Vision;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;
using Unity.Transforms;

namespace HyperRTS.Network.Relevancy
{
    /// <summary>
    /// Server-side fog of war: owned ghosts hidden from a client's team are marked irrelevant to it, so a hacked client
    /// has nothing hidden to reveal. Ghosts without an owner (players, the match) always replicate; observers see all.
    /// </summary>
    [BurstCompile]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup), OrderLast = true)]
    [UpdateAfter(typeof(EndSimulationEntityCommandBufferSystem))]
    [UpdateBefore(typeof(GhostSendSystem))]
    public partial struct FogRelevancySystem : ISystem
    {
        private struct Viewer
        {
            public int NetworkId;
            public byte Faction;
        }

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<GhostRelevancy>();
            state.RequireForUpdate<MapSettings>();
            state.RequireForUpdate<FactionRelations>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            ref var relevancy = ref SystemAPI.GetSingletonRW<GhostRelevancy>().ValueRW;
            relevancy.GhostRelevancySet.Clear();
            if (!SystemAPI.GetSingleton<MapSettings>().FogOfWar || !SystemAPI.TryGetSingleton<FogOfWar>(out var fog))
            {
                relevancy.GhostRelevancyMode = GhostRelevancyMode.Disabled;
                return;
            }

            // Only hostile, hidden pairs are listed, so observers and allies cost nothing.
            relevancy.GhostRelevancyMode = GhostRelevancyMode.SetIsIrrelevant;
            var relations = SystemAPI.GetSingleton<FactionRelations>();
            var viewers = Viewers(ref state);
            foreach (var (transform, owner, ghost) in
                     SystemAPI.Query<RefRO<LocalTransform>, RefRO<Faction>, RefRO<GhostInstance>>())
            {
                var position = transform.ValueRO.Position;
                var faction = owner.ValueRO.Value;
                foreach (var viewer in viewers)
                {
                    if (fog.IsHiddenFrom(relations, viewer.Faction, faction, position))
                    {
                        relevancy.GhostRelevancySet.TryAdd(
                            new RelevantGhostForConnection(viewer.NetworkId, ghost.ValueRO.ghostId), 1);
                    }
                }
            }
        }

        /// <summary>In-game connections holding a slot, with its faction; observers see all, so are left out.</summary>
        private NativeList<Viewer> Viewers(ref SystemState state)
        {
            var players = PlayerConnections.ByNetworkId(SystemAPI.QueryBuilder().WithAll<PlayerConnection>().Build());
            var viewers = new NativeList<Viewer>(8, Allocator.Temp);
            foreach (var id in SystemAPI.Query<RefRO<NetworkId>>().WithAll<NetworkStreamInGame>())
            {
                if (players.TryGetValue(id.ValueRO.Value, out var player))
                {
                    var faction = SystemAPI.GetComponent<Player>(player).Faction;
                    viewers.Add(new Viewer { NetworkId = id.ValueRO.Value, Faction = faction });
                }
            }

            return viewers;
        }
    }
}
