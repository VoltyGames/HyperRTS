using Unity.Collections;
using Unity.Entities;
using Unity.Netcode;

namespace HyperRTS.Network.Common
{
    /// <summary>
    /// The received <typeparamref name="T"/> RPCs of a world. <see cref="Take(EntityManager)"/> copies them out and
    /// destroys their request entities in one step, so a receiver can't leave any behind.
    /// </summary>
    public struct RpcInbox<T> where T : unmanaged, IRpcCommand
    {
        private EntityQuery _requests;

        public RpcInbox(ref SystemState state) =>
            _requests = new EntityQueryBuilder(Allocator.Temp).WithAll<T, ReceiveRpcCommandRequest>().Build(ref state);

        /// <summary>For <c>RequireForUpdate</c>, so a receiver sleeps while nothing arrives.</summary>
        public readonly EntityQuery Requests => _requests;

        public readonly bool IsEmpty => _requests.IsEmpty;

        /// <summary>
        /// Every received RPC in arrival order; their request entities are gone afterwards. That is a structural
        /// change, so take before fetching buffers or updating lookups.
        /// </summary>
        public readonly NativeArray<T> Take(EntityManager entityManager)
        {
            var rpcs = _requests.ToComponentDataArray<T>(Allocator.Temp);
            entityManager.DestroyEntity(_requests);
            return rpcs;
        }

        /// <summary>As <see cref="Take(EntityManager)"/>, with the connection each RPC came from.</summary>
        public readonly NativeArray<T> Take(EntityManager entityManager, out NativeArray<Entity> senders)
        {
            var requests = _requests.ToComponentDataArray<ReceiveRpcCommandRequest>(Allocator.Temp);
            senders = new NativeArray<Entity>(requests.Length, Allocator.Temp);
            for (var i = 0; i < requests.Length; i++)
            {
                senders[i] = requests[i].SourceConnection;
            }

            return Take(entityManager);
        }
    }
}
