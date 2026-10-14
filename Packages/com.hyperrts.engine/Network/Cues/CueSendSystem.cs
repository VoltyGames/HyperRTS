using HyperRTS.Core;
using HyperRTS.Network.Common;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Cues;
using HyperRTS.Simulation.Vision;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace HyperRTS.Network.Cues
{
    /// <summary>
    /// Server: sends each client the frame's cues it may hear or see (<see cref="CueRules.IsVisible"/>), batched into
    /// <see cref="CueRpc"/>s once per viewer faction.
    /// </summary>
    [BurstCompile]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(LifecycleSystemGroup), OrderLast = true)]
    [UpdateAfter(typeof(ViewerFanoutSystem))]
    public partial struct CueSendSystem : ISystem
    {
        /// <summary>
        /// Most batches each client gets per tick; a big battle drops the rest instead of flooding RPCs.
        /// </summary>
        public const int MaxBatchesPerTick = 2;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<CueQueue>();
            state.RequireForUpdate<FactionRelations>();
            state.RequireForUpdate<ViewerFanout>();
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
            var fanout = SystemAPI.GetSingleton<ViewerFanout>();
            foreach (var viewer in fanout.Factions)
            {
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
                fanout.Send(ecb, viewer, batches);
            }
        }
    }
}
