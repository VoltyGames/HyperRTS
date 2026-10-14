using HyperRTS.Core;
using HyperRTS.Simulation.Cues;
using Unity.Burst;
using Unity.Entities;
using Unity.NetCode;

namespace HyperRTS.Network.Cues
{
    /// <summary>Client: unpacks the server's cue batches into local <see cref="CueEvent"/>s, so playback reads one buffer.</summary>
    [BurstCompile]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    [UpdateInGroup(typeof(OrderSystemGroup), OrderFirst = true)]
    [UpdateAfter(typeof(CueClearSystem))]
    public partial struct CueReceiveSystem : ISystem
    {
        private EntityQuery _rpcs;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _rpcs = SystemAPI.QueryBuilder().WithAll<CueRpc, ReceiveRpcCommandRequest>().Build();
            state.RequireForUpdate(_rpcs);
            state.RequireForUpdate<CueQueue>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var cues = SystemAPI.GetSingletonBuffer<CueEvent>();
            foreach (var rpc in SystemAPI.Query<RefRO<CueRpc>>().WithAll<ReceiveRpcCommandRequest>())
            {
                foreach (var packed in rpc.ValueRO.Cues)
                {
                    cues.Add(packed.Unpack());
                }
            }

            state.EntityManager.DestroyEntity(_rpcs);
        }
    }
}
