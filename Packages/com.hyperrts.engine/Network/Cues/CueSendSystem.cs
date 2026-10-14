using HyperRTS.Core;
using HyperRTS.Network.Players;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Cues;
using HyperRTS.Simulation.Vision;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;

namespace HyperRTS.Network.Cues
{
    /// <summary>
    /// Server: sends each client the frame's cues it may hear or see (<see cref="CueRules.IsVisible"/>), batched into
    /// <see cref="CueRpc"/>s.
    /// </summary>
    [BurstCompile]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(LifecycleSystemGroup), OrderLast = true)]
    public partial struct CueSendSystem : ISystem
    {
        /// <summary>
        /// Most batches one client gets per tick; a big battle drops the rest instead of flooding RPCs.
        /// </summary>
        public const int MaxBatchesPerTick = 2;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<CueQueue>();
            state.RequireForUpdate<FactionRelations>();
            state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var cues = SystemAPI.GetSingletonBuffer<CueEvent>(true);
            if (cues.IsEmpty)
            {
                return;
            }

            var relations = SystemAPI.GetSingleton<FactionRelations>();
            SystemAPI.TryGetSingleton(out FogOfWar fog);
            var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
                .CreateCommandBuffer(state.WorldUnmanaged);
            var visible = new NativeList<CueEvent>(cues.Length, Allocator.Temp);
            var batches = new NativeList<CueRpc>(MaxBatchesPerTick, Allocator.Temp);
            foreach (var (_, connection) in SystemAPI.Query<RefRO<NetworkId>>().WithAll<NetworkStreamInGame>()
                         .WithEntityAccess())
            {
                var viewer = ConnectionPlayers.Faction(ref state, connection);
                visible.Clear();
                foreach (var cue in cues)
                {
                    if (CueRules.IsVisible(cue, viewer, fog, relations))
                    {
                        visible.Add(cue);
                    }
                }

                batches.Clear();
                CuePacking.Batch(visible.AsArray(), MaxBatchesPerTick, batches);
                foreach (var batch in batches)
                {
                    var rpc = ecb.CreateEntity();
                    ecb.AddComponent(rpc, batch);
                    ecb.AddComponent(rpc, new SendRpcCommandRequest { TargetConnection = connection });
                }
            }
        }
    }
}
