using HyperRTS.Core;
using HyperRTS.Network.Players;
using HyperRTS.Simulation.Combat;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Vision;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;

namespace HyperRTS.Network.Combat
{
    /// <summary>
    /// Server: sends each client the frame's impacts it may see (<see cref="ImpactRules.IsVisible"/>: its own side's,
    /// and enemies' where its team has sight), batched into <see cref="ImpactRpc"/>s. The server itself draws nothing.
    /// </summary>
    [BurstCompile]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(LifecycleSystemGroup), OrderLast = true)]
    public partial struct ImpactSendSystem : ISystem
    {
        /// <summary>Most batches one client gets per tick; a barrage drops the rest instead of flooding RPCs.</summary>
        public const int MaxBatchesPerTick = 1;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<ImpactQueue>();
            state.RequireForUpdate<FactionRelations>();
            state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var impacts = SystemAPI.GetSingletonBuffer<ImpactEvent>(true);
            if (impacts.IsEmpty)
            {
                return;
            }

            var relations = SystemAPI.GetSingleton<FactionRelations>();
            SystemAPI.TryGetSingleton(out FogOfWar fog);
            var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
                .CreateCommandBuffer(state.WorldUnmanaged);
            var visible = new NativeList<ImpactEvent>(impacts.Length, Allocator.Temp);
            var batches = new NativeList<ImpactRpc>(MaxBatchesPerTick, Allocator.Temp);
            foreach (var (_, connection) in SystemAPI.Query<RefRO<NetworkId>>().WithAll<NetworkStreamInGame>()
                         .WithEntityAccess())
            {
                var viewer = ConnectionPlayers.Faction(ref state, connection);
                visible.Clear();
                foreach (var impact in impacts)
                {
                    if (ImpactRules.IsVisible(impact, viewer, fog, relations))
                    {
                        visible.Add(impact);
                    }
                }

                batches.Clear();
                ImpactRpc.Batch(visible.AsArray(), MaxBatchesPerTick, batches);
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
