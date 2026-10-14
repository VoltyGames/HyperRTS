using HyperRTS.Core;
using HyperRTS.Simulation.Combat;
using Unity.Burst;
using Unity.Entities;
using Unity.NetCode;

namespace HyperRTS.Network.Combat
{
    /// <summary>Client: unpacks the server's impact batches into local <see cref="ImpactEvent"/>s, so presentation reads one buffer.</summary>
    [BurstCompile]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    [UpdateInGroup(typeof(OrderSystemGroup), OrderFirst = true)]
    [UpdateAfter(typeof(ImpactClearSystem))]
    public partial struct ImpactReceiveSystem : ISystem
    {
        private EntityQuery _rpcs;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _rpcs = SystemAPI.QueryBuilder().WithAll<ImpactRpc, ReceiveRpcCommandRequest>().Build();
            state.RequireForUpdate(_rpcs);
            state.RequireForUpdate<ImpactQueue>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var impacts = SystemAPI.GetSingletonBuffer<ImpactEvent>();
            foreach (var rpc in SystemAPI.Query<RefRO<ImpactRpc>>().WithAll<ReceiveRpcCommandRequest>())
            {
                foreach (var packed in rpc.ValueRO.Impacts)
                {
                    impacts.Add(packed.Unpack());
                }
            }

            state.EntityManager.DestroyEntity(_rpcs);
        }
    }
}
