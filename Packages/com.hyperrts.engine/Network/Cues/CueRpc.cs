using HyperRTS.Simulation.Cues;
using Unity.Collections;
using Unity.Mathematics;
using Unity.NetCode;

namespace HyperRTS.Network.Cues
{
    /// <summary>A batch of <c>CueEvent</c>s; the client finds each sound, rig and mark through its own copy of the prefab.</summary>
    public struct CueRpc : IRpcCommand
    {
        /// <summary>48 events at 18 bytes keep the RPC under 1 KB on the wire.</summary>
        public const int Capacity = 48;

        [GhostFixedListCapacity(Capacity = Capacity)]
        public FixedList4096Bytes<PackedCue> Cues;

        /// <summary>Fills up to <paramref name="maxBatches"/> RPCs of <see cref="Capacity"/>; the rest is dropped.</summary>
        public static void Batch(NativeArray<CueEvent> cues, int maxBatches, NativeList<CueRpc> batches)
        {
            var count = math.min(cues.Length, maxBatches * Capacity);
            for (var start = 0; start < count; start += Capacity)
            {
                var rpc = new CueRpc();
                var end = math.min(start + Capacity, count);
                for (var i = start; i < end; i++)
                {
                    rpc.Cues.Add(PackedCue.Pack(cues[i]));
                }

                batches.Add(rpc);
            }
        }
    }
}
