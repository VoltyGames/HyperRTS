using HyperRTS.Core;
using HyperRTS.Network.Common;
using HyperRTS.Simulation.Cues;
using Unity.Burst;
using Unity.Entities;

namespace HyperRTS.Network.Cues
{
    /// <summary>Client: unpacks the server's cue RPCs into local <see cref="CueEvent"/>s, so playback reads one buffer.</summary>
    [BurstCompile]
    [RemoteClientOnly]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    [UpdateInGroup(typeof(OrderSystemGroup), OrderFirst = true)]
    [UpdateAfter(typeof(CueClearSystem))]
    public partial struct CueReceiveSystem : ISystem
    {
        private RpcInbox<CueRpc> _inbox;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _inbox = new RpcInbox<CueRpc>(ref state);
            state.RequireForUpdate(_inbox.Requests);
            state.RequireForUpdate<CueQueue>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // Taking destroys entities, which would invalidate a buffer fetched before it.
            var rpcs = _inbox.Take(state.EntityManager);
            var cues = SystemAPI.GetSingletonBuffer<CueEvent>();
            foreach (var rpc in rpcs)
            {
                foreach (var packed in rpc.Cues)
                {
                    cues.Add(packed.Unpack());
                }
            }
        }
    }
}
