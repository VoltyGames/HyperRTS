using HyperRTS.Core;
using HyperRTS.Network.Players;
using HyperRTS.Simulation.Combat;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Vision;
using Unity.Burst;
using Unity.Entities;
using Unity.NetCode;

namespace HyperRTS.Network.Combat
{
    /// <summary>
    /// Server: sends each client the frame's impacts it may see (<see cref="ImpactRules.IsVisible"/>: its own side's,
    /// and enemies' where its team has sight). The server itself draws nothing.
    /// </summary>
    [BurstCompile]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(LifecycleSystemGroup), OrderLast = true)]
    public partial struct ImpactSendSystem : ISystem
    {
        /// <summary>Most impacts one client gets per tick; a barrage drops the rest instead of flooding RPCs.</summary>
        public const int MaxPerTick = 16;

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
            foreach (var (_, connection) in SystemAPI.Query<RefRO<NetworkId>>().WithAll<NetworkStreamInGame>()
                         .WithEntityAccess())
            {
                var viewer = ConnectionPlayers.Faction(ref state, connection);
                var sent = 0;
                for (var i = 0; i < impacts.Length && sent < MaxPerTick; i++)
                {
                    if (!ImpactRules.IsVisible(impacts[i], viewer, fog, relations))
                    {
                        continue;
                    }

                    var rpc = ecb.CreateEntity();
                    ecb.AddComponent(rpc, ToRpc(impacts[i]));
                    ecb.AddComponent(rpc, new SendRpcCommandRequest { TargetConnection = connection });
                    sent++;
                }
            }
        }

        private static ImpactRpc ToRpc(in ImpactEvent impact) => new()
        {
            TypeId = impact.TypeId,
            Position = impact.Position,
            Radius = impact.Radius,
            Faction = impact.Faction,
            Surface = (byte)impact.Surface,
            Material = (byte)impact.Material,
        };
    }
}
