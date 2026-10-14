using HyperRTS.Core;
using HyperRTS.Simulation.Effects;
using Unity.Burst;
using Unity.Entities;
using Unity.NetCode;

namespace HyperRTS.Network.Effects
{
    /// <summary>Client: unpacks the server's effect batches into local <see cref="EffectEvent"/>s, so presentation reads one buffer.</summary>
    [BurstCompile]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    [UpdateInGroup(typeof(OrderSystemGroup), OrderFirst = true)]
    [UpdateAfter(typeof(EffectClearSystem))]
    public partial struct EffectReceiveSystem : ISystem
    {
        private EntityQuery _rpcs;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _rpcs = SystemAPI.QueryBuilder().WithAll<EffectRpc, ReceiveRpcCommandRequest>().Build();
            state.RequireForUpdate(_rpcs);
            state.RequireForUpdate<EffectQueue>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var effects = SystemAPI.GetSingletonBuffer<EffectEvent>();
            foreach (var rpc in SystemAPI.Query<RefRO<EffectRpc>>().WithAll<ReceiveRpcCommandRequest>())
            {
                foreach (var packed in rpc.ValueRO.Effects)
                {
                    effects.Add(EffectPacking.Unpack(packed));
                }
            }

            state.EntityManager.DestroyEntity(_rpcs);
        }
    }
}
