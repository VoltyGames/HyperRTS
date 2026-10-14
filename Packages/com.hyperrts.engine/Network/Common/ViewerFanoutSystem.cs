using HyperRTS.Core;
using HyperRTS.Network.Players;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;

namespace HyperRTS.Network.Common
{
    /// <summary>Server: regroups the in-game connections into the <see cref="ViewerFanout"/> before the senders run.</summary>
    [BurstCompile]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(LifecycleSystemGroup), OrderLast = true)]
    public partial struct ViewerFanoutSystem : ISystem
    {
        private ViewerFanout _fanout;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _fanout = ViewerFanout.Create(Allocator.Persistent);
            state.EntityManager.CreateSingleton(_fanout);
        }

        public void OnDestroy(ref SystemState state) => _fanout.Dispose();

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _fanout.Clear();
            foreach (var (_, connection) in SystemAPI.Query<RefRO<NetworkId>>().WithAll<NetworkStreamInGame>()
                         .WithEntityAccess())
            {
                _fanout.Add(ConnectionPlayers.Faction(ref state, connection), connection);
            }
        }
    }
}
