using Unity.Collections;
using Unity.Entities;
using Unity.Netcode;

namespace HyperRTS.Network.Common
{
    /// <summary>
    /// Queues RPCs as Netcode's send requests. <c>Entity.Null</c> sends to every remote connection, never to a host's
    /// own player, which shares the sender's world.
    /// </summary>
    public static class Rpc
    {
        public static void Send<T>(EntityCommandBuffer ecb, Entity connection, in T rpc)
            where T : unmanaged, IRpcCommand
        {
            var request = ecb.CreateEntity();
            ecb.AddComponent(request, rpc);
            ecb.AddComponent(request, Request(connection));
        }

        public static void Send<T>(EntityCommandBuffer ecb, Entity connection, NativeList<T> rpcs)
            where T : unmanaged, IRpcCommand
        {
            foreach (var rpc in rpcs)
            {
                Send(ecb, connection, rpc);
            }
        }

        public static void Send<T>(EntityManager entityManager, Entity connection, in T rpc)
            where T : unmanaged, IRpcCommand
        {
            var request = entityManager.CreateEntity();
            entityManager.AddComponentData(request, rpc);
            entityManager.AddComponentData(request, Request(connection));
        }

        private static SendRpcCommandRequest Request(Entity connection)
        {
            var targets = connection == Entity.Null ? RpcBroadcastTargets.NonLocal : RpcBroadcastTargets.All;
            return new SendRpcCommandRequest { TargetConnection = connection, BroadcastTargets = targets };
        }
    }
}
