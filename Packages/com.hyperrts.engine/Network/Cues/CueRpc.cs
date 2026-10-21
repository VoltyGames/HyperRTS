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

        /// <summary>
        /// Fills up to <paramref name="maxBatches"/> RPCs of <see cref="Capacity"/> in arrival order. Past the limit,
        /// per-shot fire and impact cues are dropped before deaths, abilities and voices.
        /// </summary>
        public static void Batch(NativeArray<CueEvent> cues, int maxBatches, NativeList<CueRpc> batches)
        {
            var quotas = Quotas(cues, maxBatches * Capacity);
            var rpc = new CueRpc();
            foreach (var cue in cues)
            {
                var priority = Priority(cue.Slot);
                if (quotas[priority] == 0)
                {
                    continue;
                }

                quotas[priority]--;
                rpc.Cues.Add(PackedCue.Pack(cue));
                if (rpc.Cues.Length == Capacity)
                {
                    batches.Add(rpc);
                    rpc = new CueRpc();
                }
            }

            if (rpc.Cues.Length > 0)
            {
                batches.Add(rpc);
            }
        }

        /// <summary>How many cues of each priority fit in <paramref name="budget"/>, most important first.</summary>
        private static int3 Quotas(NativeArray<CueEvent> cues, int budget)
        {
            var counts = int3.zero;
            foreach (var cue in cues)
            {
                counts[Priority(cue.Slot)]++;
            }

            var quotas = int3.zero;
            for (var priority = 0; priority < 3; priority++)
            {
                quotas[priority] = math.min(counts[priority], budget);
                budget -= quotas[priority];
            }

            return quotas;
        }

        // A battle fires and lands far more shots than anything else, and losing one of them goes unnoticed.
        private static int Priority(CueSlot slot) => slot == CueSlot.Fire ? 2 : slot == CueSlot.Impact ? 1 : 0;
    }
}
