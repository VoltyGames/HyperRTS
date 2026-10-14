using HyperRTS.Simulation.Combat;
using HyperRTS.Simulation.Replays;
using Unity.Collections;
using Unity.Mathematics;
using Unity.NetCode;

namespace HyperRTS.Network.Combat
{
    /// <summary>A batch of <c>ImpactEvent</c>s; the client finds the marks through its own copy of the prefab.</summary>
    public struct ImpactRpc : IRpcCommand
    {
        /// <summary>64 impacts at 15 bytes keep the RPC under 1 KB on the wire.</summary>
        public const int Capacity = 64;

        [GhostFixedListCapacity(Capacity = Capacity)]
        public FixedList4096Bytes<PackedImpact> Impacts;

        /// <summary>Fills up to <paramref name="maxBatches"/> RPCs of <see cref="Capacity"/>; the rest is dropped.</summary>
        public static void Batch(NativeArray<ImpactEvent> impacts, int maxBatches, NativeList<ImpactRpc> batches)
        {
            var count = math.min(impacts.Length, maxBatches * Capacity);
            for (var start = 0; start < count; start += Capacity)
            {
                var rpc = new ImpactRpc();
                var end = math.min(start + Capacity, count);
                for (var i = start; i < end; i++)
                {
                    rpc.Impacts.Add(PackedImpact.Pack(impacts[i]));
                }

                batches.Add(rpc);
            }
        }
    }
}
