using HyperRTS.Network.Players;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Vision;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;
using Unity.Netcode;
using Unity.Profiling;
using Unity.Transforms;

namespace HyperRTS.Network.Replication
{
    /// <summary>
    /// Server-side fog of war: owned ghosts hidden from a client's team (by fog or undetected stealth) are marked
    /// irrelevant to it, so a hacked client has nothing hidden to reveal. Ghosts without an owner (players, the match)
    /// always replicate, and so do <see cref="Destructible"/> map buildings, which fog never hides whoever holds them
    /// (irrelevant, they would vanish from the map). Observers see all.
    /// </summary>
    [BurstCompile]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup), OrderLast = true)]
    [UpdateAfter(typeof(EndSimulationEntityCommandBufferSystem))]
    [UpdateBefore(typeof(GhostSendSystem))]
    public partial struct FogRelevancySystem : ISystem
    {
        private static readonly ProfilerMarker HideMarker = new("FogRelevancySystem.Hide");

        private EntityQuery _ghosts;
        private ConcealmentLookup _concealment;

        private struct Viewer
        {
            public int NetworkId;
            public byte Faction;
        }

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _ghosts = SystemAPI.QueryBuilder().WithAll<LocalTransform, Faction, GhostInstance>()
                .WithNone<Destructible>().Build();
            _concealment = new ConcealmentLookup(ref state);
            state.RequireForUpdate<GhostRelevancy>();
            state.RequireForUpdate<MapSettings>();
            state.RequireForUpdate<FactionRelations>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            ref var relevancy = ref SystemAPI.GetSingletonRW<GhostRelevancy>().ValueRW;
            var hidden = relevancy.GhostRelevancySet;
            hidden.Clear();
            if (!SystemAPI.TryGetSingleton<FogOfWar>(out var fog))
            {
                relevancy.GhostRelevancyMode = GhostRelevancyMode.Disabled;
                return;
            }

            // Only hostile, hidden pairs are listed, so observers and allies cost nothing.
            relevancy.GhostRelevancyMode = GhostRelevancyMode.SetIsIrrelevant;
            var viewers = Viewers(ref state);
            if (viewers.IsEmpty)
            {
                return;
            }

            // A parallel writer can't grow the set.
            var most = _ghosts.CalculateEntityCount() * viewers.Length;
            if (hidden.Capacity < most)
            {
                hidden.Capacity = most;
            }

            _concealment.Update(ref state);
            state.Dependency = new HideJob
            {
                Fog = fog,
                Concealments = _concealment,
                Viewers = viewers.AsArray(),
                Relations = SystemAPI.GetSingleton<FactionRelations>(),
                FogOn = SystemAPI.GetSingleton<MapSettings>().FogOfWar,
                Hidden = hidden.AsParallelWriter(),
            }.ScheduleParallel(_ghosts, state.Dependency);
        }

        /// <summary>In-game connections holding a slot, with its faction; observers see all, so are left out.</summary>
        private NativeList<Viewer> Viewers(ref SystemState state)
        {
            var viewers = new NativeList<Viewer>(8, state.WorldUpdateAllocator);
            // A host's own connection has no stream: its world holds every ghost, and presentation fog hides enemies.
            foreach (var (id, link) in SystemAPI.Query<RefRO<NetworkId>, RefRO<ConnectionPlayer>>()
                         .WithAll<NetworkStreamInGame, NetworkStreamConnection>())
            {
                var faction = SystemAPI.GetComponent<Player>(link.ValueRO.Player).Faction;
                viewers.Add(new Viewer { NetworkId = id.ValueRO.Value, Faction = faction });
            }

            return viewers;
        }

        [BurstCompile]
        private partial struct HideJob : IJobEntity, IJobEntityChunkBeginEnd
        {
            [ReadOnly] public FogOfWar Fog;
            [ReadOnly] public ConcealmentLookup Concealments;
            [ReadOnly] public NativeArray<Viewer> Viewers;
            public FactionRelations Relations;
            public bool FogOn;
            public NativeParallelHashMap<RelevantGhostForConnection, int>.ParallelWriter Hidden;

            // Per chunk, not per ghost, so the marker costs little.
            public bool OnChunkBegin(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask,
                in v128 chunkEnabledMask)
            {
                HideMarker.Begin();
                return true;
            }

            public void OnChunkEnd(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask,
                in v128 chunkEnabledMask, bool chunkWasExecuted) => HideMarker.End();

            private void Execute(Entity entity, in LocalTransform transform, in Faction owner, in GhostInstance ghost)
            {
                // With fog off only stealth hides anything.
                var concealment = Concealments.Find(entity);
                if (!FogOn && concealment == Concealment.None)
                {
                    return;
                }

                var tall = Concealments.IsTall(entity);
                foreach (var viewer in Viewers)
                {
                    if (Fog.IsHiddenFrom(Relations, viewer.Faction, owner.Value, transform.Position, concealment, tall))
                    {
                        Hidden.TryAdd(new RelevantGhostForConnection(viewer.NetworkId, ghost.ghostId), 1);
                    }
                }
            }
        }
    }
}
