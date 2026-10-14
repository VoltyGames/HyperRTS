using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Cues;
using Unity.Collections;
using Unity.Mathematics;

namespace HyperRTS.Network.Cues
{
    /// <summary>Packs <see cref="CueEvent"/>s into <see cref="CueRpc"/> batches and back.</summary>
    public static class CuePacking
    {
        private const float PitchSteps = 127f;

        public static PackedCue Pack(in CueEvent cue)
        {
            var direction = math.normalizesafe(cue.Direction, new float3(0f, 0f, 1f));
            var pitch = math.asin(math.clamp(direction.y, -1f, 1f)) / math.PI + 0.5f;
            var flags = cue.Stealthed ? PackedCue.StealthedBit : (byte)0;
            return new PackedCue
            {
                TypeId = cue.TypeId,
                Position = cue.Position,
                Slot = (byte)cue.Slot,
                Faction = cue.Faction,
                Yaw = HeadingByte.Encode(direction.xz),
                PitchFlags = (byte)(flags | (int)math.round(pitch * PitchSteps)),
            };
        }

        public static CueEvent Unpack(in PackedCue packed)
        {
            var pitchStep = packed.PitchFlags & ~PackedCue.StealthedBit;
            var pitch = (pitchStep / PitchSteps - 0.5f) * math.PI;
            var level = HeadingByte.Direction(packed.Yaw);
            math.sincos(pitch, out var sinPitch, out var cosPitch);
            return new CueEvent
            {
                TypeId = packed.TypeId,
                Slot = (CueSlot)packed.Slot,
                Faction = packed.Faction,
                Position = packed.Position,
                Direction = new float3(level.x * cosPitch, sinPitch, level.z * cosPitch),
                Stealthed = (packed.PitchFlags & PackedCue.StealthedBit) != 0,
            };
        }

        /// <summary>Fills up to <paramref name="maxBatches"/> RPCs of <see cref="CueRpc.Capacity"/>; the rest is dropped.</summary>
        public static void Batch(NativeArray<CueEvent> cues, int maxBatches, NativeList<CueRpc> batches)
        {
            var count = math.min(cues.Length, maxBatches * CueRpc.Capacity);
            for (var start = 0; start < count; start += CueRpc.Capacity)
            {
                var rpc = new CueRpc();
                var end = math.min(start + CueRpc.Capacity, count);
                for (var i = start; i < end; i++)
                {
                    rpc.Cues.Add(Pack(cues[i]));
                }

                batches.Add(rpc);
            }
        }
    }
}
