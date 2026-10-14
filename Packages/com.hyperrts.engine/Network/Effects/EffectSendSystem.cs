using HyperRTS.Core;
using HyperRTS.Network.Players;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Effects;
using HyperRTS.Simulation.Vision;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;

namespace HyperRTS.Network.Effects
{
    /// <summary>
    /// Server: sends each client the frame's effects it may see, batched into <see cref="EffectRpc"/>s.
    /// </summary>
    [BurstCompile]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(LifecycleSystemGroup), OrderLast = true)]
    public partial struct EffectSendSystem : ISystem
    {
        /// <summary>
        /// Most batches one client gets per tick; a big battle drops the rest instead of flooding RPCs.
        /// </summary>
        public const int MaxBatchesPerTick = 2;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<EffectQueue>();
            state.RequireForUpdate<FactionRelations>();
            state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var effects = SystemAPI.GetSingletonBuffer<EffectEvent>(true);
            if (effects.IsEmpty)
            {
                return;
            }

            var relations = SystemAPI.GetSingleton<FactionRelations>();
            SystemAPI.TryGetSingleton(out FogOfWar fog);
            var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
                .CreateCommandBuffer(state.WorldUnmanaged);
            var visible = new NativeList<EffectEvent>(effects.Length, Allocator.Temp);
            var batches = new NativeList<EffectRpc>(MaxBatchesPerTick, Allocator.Temp);
            foreach (var (_, connection) in SystemAPI.Query<RefRO<NetworkId>>().WithAll<NetworkStreamInGame>()
                         .WithEntityAccess())
            {
                var viewer = ConnectionPlayers.Faction(ref state, connection);
                visible.Clear();
                foreach (var effect in effects)
                {
                    if (EffectRules.IsVisible(effect, viewer, fog, relations))
                    {
                        visible.Add(effect);
                    }
                }

                batches.Clear();
                EffectPacking.Batch(visible.AsArray(), MaxBatchesPerTick, batches);
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
