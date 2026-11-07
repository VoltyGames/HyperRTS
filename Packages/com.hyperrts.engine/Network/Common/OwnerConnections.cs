using Unity.Collections;
using Unity.Entities;
using Unity.Netcode;

namespace HyperRTS.Network.Common
{
    /// <summary>
    /// Server singleton: this tick's in-game connections grouped by viewer owner (observers under neutral), so event
    /// senders filter and pack once per owner instead of once per connection.
    /// </summary>
    public struct OwnerConnections : IComponentData
    {
        /// <summary>Each distinct viewer owner once.</summary>
        public NativeList<byte> Owners;

        public NativeParallelMultiHashMap<byte, Entity> Connections;

        public static OwnerConnections Create(Allocator allocator) => new()
        {
            Owners = new NativeList<byte>(8, allocator),
            Connections = new NativeParallelMultiHashMap<byte, Entity>(16, allocator),
        };

        public void Dispose()
        {
            Owners.Dispose();
            Connections.Dispose();
        }

        public void Clear()
        {
            Owners.Clear();
            Connections.Clear();
        }

        public void Add(byte owner, Entity connection)
        {
            if (!Connections.ContainsKey(owner))
            {
                Owners.Add(owner);
            }

            Connections.Add(owner, connection);
        }

        /// <summary>Queues every RPC to each connection viewing as <paramref name="owner"/>.</summary>
        public readonly void Send<T>(EntityCommandBuffer ecb, byte owner, NativeList<T> rpcs)
            where T : unmanaged, IRpcCommand
        {
            foreach (var connection in Connections.GetValuesForKey(owner))
            {
                Rpc.Send(ecb, connection, rpcs);
            }
        }
    }
}
