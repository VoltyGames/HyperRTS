using Unity.Collections;
using Unity.Entities;
using Unity.Netcode;

namespace HyperRTS.Network.Common
{
    /// <summary>
    /// Server singleton: this tick's in-game connections grouped by viewer faction (observers under neutral), so event
    /// senders filter and pack once per faction instead of once per connection.
    /// </summary>
    public struct FactionConnections : IComponentData
    {
        /// <summary>Each distinct viewer faction once.</summary>
        public NativeList<byte> Factions;

        public NativeParallelMultiHashMap<byte, Entity> Connections;

        public static FactionConnections Create(Allocator allocator) => new()
        {
            Factions = new NativeList<byte>(8, allocator),
            Connections = new NativeParallelMultiHashMap<byte, Entity>(16, allocator),
        };

        public void Dispose()
        {
            Factions.Dispose();
            Connections.Dispose();
        }

        public void Clear()
        {
            Factions.Clear();
            Connections.Clear();
        }

        public void Add(byte faction, Entity connection)
        {
            if (!Connections.ContainsKey(faction))
            {
                Factions.Add(faction);
            }

            Connections.Add(faction, connection);
        }

        /// <summary>Queues every batch as an RPC to each connection viewing as <paramref name="faction"/>.</summary>
        public readonly void Send<T>(EntityCommandBuffer ecb, byte faction, NativeList<T> batches)
            where T : unmanaged, IRpcCommand
        {
            foreach (var connection in Connections.GetValuesForKey(faction))
            {
                Send(ecb, connection, batches);
            }
        }

        /// <summary>Queues every batch as an RPC to <paramref name="connection"/>.</summary>
        public static void Send<T>(EntityCommandBuffer ecb, Entity connection, NativeList<T> batches)
            where T : unmanaged, IRpcCommand
        {
            foreach (var batch in batches)
            {
                var rpc = ecb.CreateEntity();
                ecb.AddComponent(rpc, batch);
                ecb.AddComponent(rpc, new SendRpcCommandRequest { TargetConnection = connection });
            }
        }
    }
}
