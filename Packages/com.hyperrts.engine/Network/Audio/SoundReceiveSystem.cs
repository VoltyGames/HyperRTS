using HyperRTS.Core;
using HyperRTS.Simulation.Audio;
using Unity.Burst;
using Unity.Entities;
using Unity.NetCode;

namespace HyperRTS.Network.Audio
{
    /// <summary>Client: queues the server's sounds as local <see cref="SoundEvent"/>s, so playback reads one buffer.</summary>
    [BurstCompile]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    [UpdateInGroup(typeof(OrderSystemGroup), OrderFirst = true)]
    [UpdateAfter(typeof(SoundClearSystem))]
    public partial struct SoundReceiveSystem : ISystem
    {
        private EntityQuery _rpcs;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _rpcs = SystemAPI.QueryBuilder().WithAll<SoundRpc, ReceiveRpcCommandRequest>().Build();
            state.RequireForUpdate(_rpcs);
            state.RequireForUpdate<SoundQueue>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var sounds = SystemAPI.GetSingletonBuffer<SoundEvent>();
            foreach (var rpc in SystemAPI.Query<RefRO<SoundRpc>>().WithAll<ReceiveRpcCommandRequest>())
            {
                sounds.Add(new SoundEvent
                {
                    TypeId = rpc.ValueRO.TypeId,
                    Slot = (SoundSlot)rpc.ValueRO.Slot,
                    Faction = rpc.ValueRO.Faction,
                    Position = rpc.ValueRO.Position,
                });
            }

            state.EntityManager.DestroyEntity(_rpcs);
        }
    }
}
