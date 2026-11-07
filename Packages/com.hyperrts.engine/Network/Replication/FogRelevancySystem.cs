using HyperRTS.Network.Joining;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Common.Players;
using HyperRTS.Simulation.Vision.Fog;
using HyperRTS.Simulation.Vision.Sight;
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
    /// Server-side fog of war: ghosts hidden from a client's team are marked irrelevant to it, so a hacked client has nothing
    /// to reveal. Ownerless ghosts, <see cref="Destructible"/> buildings and observers see everything.
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
            public byte Owner;
        }

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _ghosts = SystemAPI.QueryBuilder().WithAll<LocalTransform, Owner, GhostInstance>()
                .WithNone<Destructible>().Build();
            _concealment = new ConcealmentLookup(ref state);
            state.RequireForUpdate<GhostRelevancy>();
            state.RequireForUpdate<MapSettings>();
            state.RequireForUpdate<OwnerRelations>();
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
                Relations = SystemAPI.GetSingleton<OwnerRelations>(),
                FogEnabled = SystemAPI.GetSingleton<MapSettings>().FogOfWar,
                Hidden = hidden.AsParallelWriter(),
            }.ScheduleParallel(_ghosts, state.Dependency);
        }

        /// <summary>In-game connections holding a slot, with its owner; observers see all, so are left out.</summary>
        private NativeList<Viewer> Viewers(ref SystemState state)
        {
            var viewers = new NativeList<Viewer>(8, state.WorldUpdateAllocator);
            // A host's own connection has no stream: its world holds every ghost, and presentation fog hides enemies.
            foreach (var (id, link) in SystemAPI.Query<RefRO<NetworkId>, RefRO<ConnectionPlayer>>()
                         .WithAll<NetworkStreamInGame, NetworkStreamConnection>())
            {
                var owner = SystemAPI.GetComponent<Player>(link.ValueRO.Player).Owner;
                viewers.Add(new Viewer { NetworkId = id.ValueRO.Value, Owner = owner });
            }

            return viewers;
        }

        [BurstCompile]
        private partial struct HideJob : IJobEntity, IJobEntityChunkBeginEnd
        {
            [ReadOnly] public FogOfWar Fog;
            [ReadOnly] public ConcealmentLookup Concealments;
            [ReadOnly] public NativeArray<Viewer> Viewers;
            public OwnerRelations Relations;
            public bool FogEnabled;
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

            private void Execute(Entity entity, in LocalTransform transform, in Owner owner, in GhostInstance ghost)
            {
                // With fog off only stealth hides anything.
                var concealment = Concealments.Find(entity);
                if (!FogEnabled && concealment == Concealment.None)
                {
                    return;
                }

                var tall = Concealments.IsTall(entity);
                foreach (var viewer in Viewers)
                {
                    if (Fog.IsHiddenFrom(Relations, viewer.Owner, owner.Value, transform.Position, concealment, tall))
                    {
                        Hidden.TryAdd(new RelevantGhostForConnection(viewer.NetworkId, ghost.ghostId), 1);
                    }
                }
            }
        }
    }
}
