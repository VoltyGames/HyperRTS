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
    /// Server-side fog of war: each client only receives the owned ghosts its team can see, so a hacked client has
    /// nothing hidden to reveal. Ghosts without an owner (players, the match) always replicate; observers see all.
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

            relevancy.GhostRelevancyMode = GhostRelevancyMode.SetIsRelevant;
            relevancy.DefaultRelevancyQuery = SystemAPI.QueryBuilder().WithAll<GhostInstance>().WithNone<Faction>()
                .Build();

            // The fog grid is written by jobs; this runs once per frame on a handful of connections.
            state.EntityManager.CompleteAllTrackedJobs();
            var relations = SystemAPI.GetSingleton<FactionRelations>();
            var viewers = Viewers(ref state);
            foreach (var (transform, owner, ghost) in
                     SystemAPI.Query<RefRO<LocalTransform>, RefRO<Faction>, RefRO<GhostInstance>>())
            {
                var position = transform.ValueRO.Position;
                var faction = owner.ValueRO.Value;
                foreach (var viewer in viewers)
                {
                    var isObserver = viewer.Faction == 0;
                    if (isObserver || !fog.IsHiddenFrom(relations, viewer.Faction, faction, position))
                    {
                        relevancy.GhostRelevancySet.TryAdd(
                            new RelevantGhostForConnection(viewer.NetworkId, ghost.ValueRO.ghostId), 1);
                    }
                }
            }
        }

        /// <summary>In-game connections with their slot's faction; 0 for observers.</summary>
        private NativeList<Viewer> Viewers(ref SystemState state)
        {
            var viewers = new NativeList<Viewer>(8, Allocator.Temp);
            foreach (var id in SystemAPI.Query<RefRO<NetworkId>>().WithAll<NetworkStreamInGame>())
            {
                var viewer = new Viewer { NetworkId = id.ValueRO.Value };
                foreach (var (player, link) in SystemAPI.Query<RefRO<Player>, RefRO<PlayerConnection>>())
                {
                    if (link.ValueRO.NetworkId == viewer.NetworkId)
                    {
                        viewer.Faction = player.ValueRO.Faction;
                    }
                }

                viewers.Add(viewer);
            }

            return viewers;
        }
    }
}
