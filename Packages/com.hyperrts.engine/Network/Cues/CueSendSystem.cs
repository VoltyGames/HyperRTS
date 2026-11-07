using HyperRTS.Network.Common;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Common.Players;
using HyperRTS.Simulation.Cues;
using HyperRTS.Simulation.Vision.Fog;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace HyperRTS.Network.Cues
{
    /// <summary>
    /// Server: sends each client the frame's cues it may hear or see (<see cref="CueRules.IsVisible"/>), batched into
    /// <see cref="CueRpc"/>s once per viewer owner.
    /// </summary>
    [BurstCompile]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(LifecycleSystemGroup), OrderLast = true)]
    [UpdateAfter(typeof(OwnerConnectionsSystem))]
    public partial struct CueSendSystem : ISystem
    {
        /// <summary>
        /// Most RPCs each client gets per tick; a big battle drops its least important cues rather than flood RPCs.
        /// </summary>
        public const int MaxRpcsPerTick = 3;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<CueQueue>();
            state.RequireForUpdate<OwnerRelations>();
            state.RequireForUpdate<OwnerConnections>();
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

            var relations = SystemAPI.GetSingleton<OwnerRelations>();
            SystemAPI.TryGetSingleton(out FogOfWar fog);
            var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
                .CreateCommandBuffer(state.WorldUnmanaged);
            var visible = new NativeList<CueEvent>(cues.Length, Allocator.Temp);
            var rpcs = new NativeList<CueRpc>(MaxRpcsPerTick, Allocator.Temp);
            var viewers = SystemAPI.GetSingleton<OwnerConnections>();
            foreach (var viewer in viewers.Owners)
            {
                visible.Clear();
                foreach (var cue in cues)
                {
                    if (CueRules.IsVisible(cue, viewer, fog, relations))
                    {
                        visible.Add(cue);
                    }
                }

                rpcs.Clear();
                CueRpc.Fill(visible.AsArray(), MaxRpcsPerTick, rpcs);
                viewers.Send(ecb, viewer, rpcs);
            }
        }
    }
}
