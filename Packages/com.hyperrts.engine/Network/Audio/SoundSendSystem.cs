using HyperRTS.Core;
using HyperRTS.Network.Players;
using HyperRTS.Simulation.Audio;
using HyperRTS.Simulation.Match;
using HyperRTS.Simulation.Vision;
using Unity.Burst;
using Unity.Entities;
using Unity.NetCode;

namespace HyperRTS.Network.Audio
{
    /// <summary>
    /// Server: sends each client the frame's sounds it may hear (<see cref="SoundRules.IsAudible"/>: what its team
    /// sees and detects, plus its own voices). The server itself never plays audio.
    /// </summary>
    [BurstCompile]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(LifecycleSystemGroup), OrderLast = true)]
    public partial struct SoundSendSystem : ISystem
    {
        /// <summary>Most sounds one client gets per tick; a big battle drops the rest instead of flooding RPCs.</summary>
        public const int MaxPerTick = 16;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<SoundQueue>();
            state.RequireForUpdate<FactionRelations>();
            state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var sounds = SystemAPI.GetSingletonBuffer<SoundEvent>(true);
            if (sounds.IsEmpty)
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
                var listener = ListenerOf(ref state, connection);
                var sent = 0;
                for (var i = 0; i < sounds.Length && sent < MaxPerTick; i++)
                {
                    if (!SoundRules.IsAudible(sounds[i], listener, fog, relations))
                    {
                        continue;
                    }

                    var rpc = ecb.CreateEntity();
                    ecb.AddComponent(rpc, ToRpc(sounds[i]));
                    ecb.AddComponent(rpc, new SendRpcCommandRequest { TargetConnection = connection });
                    sent++;
                }
            }
        }

        /// <summary>The connection's faction; observers (no slot) hear every world sound.</summary>
        private byte ListenerOf(ref SystemState state, Entity connection)
        {
            if (!SystemAPI.HasComponent<ConnectionPlayer>(connection))
            {
                return Faction.Neutral;
            }

            return SystemAPI.GetComponent<Player>(SystemAPI.GetComponent<ConnectionPlayer>(connection).Player).Faction;
        }

        private static SoundRpc ToRpc(in SoundEvent sound) => new()
        {
            TypeId = sound.TypeId, Slot = (byte)sound.Slot, Faction = sound.Faction, Position = sound.Position,
        };
    }
}
