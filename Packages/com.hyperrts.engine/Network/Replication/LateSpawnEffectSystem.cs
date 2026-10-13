using HyperRTS.Core;
using HyperRTS.Simulation.Combat;
using Unity.Burst;
using Unity.Entities;
using Unity.NetCode;

namespace HyperRTS.Network.Replication
{
    /// <summary>
    /// Client: a ghost first seen well after the server spawned it (a late join, a ruin found in the fog) shows its
    /// result without its <see cref="SpawnEffect"/>, so nobody gets dust from a collapse they never saw.
    /// </summary>
    [BurstCompile]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    [UpdateInGroup(typeof(OrderSystemGroup), OrderFirst = true)]
    public partial struct LateSpawnEffectSystem : ISystem
    {
        private const float LateSeconds = 1f;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<NetworkTime>();
            state.RequireForUpdate<ClientServerTickRate>();
            state.RequireForUpdate<SpawnEffect>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var shown = SystemAPI.GetSingleton<NetworkTime>().InterpolationTick;
            if (!shown.IsValid)
            {
                return;
            }

            var late = (int)(SystemAPI.GetSingleton<ClientServerTickRate>().SimulationTickRate * LateSeconds);
            foreach (var (ghost, pending) in SystemAPI.Query<RefRO<GhostInstance>, EnabledRefRW<SpawnEffect>>())
            {
                var spawned = ghost.ValueRO.spawnTick;
                if (spawned.IsValid && shown.TicksSince(spawned) > late)
                {
                    pending.ValueRW = false;
                }
            }
        }
    }
}
