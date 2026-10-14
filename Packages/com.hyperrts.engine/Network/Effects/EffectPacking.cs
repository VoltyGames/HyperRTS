using HyperRTS.Simulation.Effects;
using Unity.Collections;
using Unity.Mathematics;

namespace HyperRTS.Network.Effects
{
    /// <summary>Packs <see cref="EffectEvent"/>s into <see cref="EffectRpc"/> batches and back.</summary>
    public static class EffectPacking
    {
        public static PackedEffect Pack(in EffectEvent effect)
        {
            var direction = math.normalizesafe(effect.Direction, new float3(0f, 0f, 1f));
            var yaw = math.atan2(direction.x, direction.z) / (2f * math.PI);
            var pitch = math.asin(math.clamp(direction.y, -1f, 1f)) / math.PI + 0.5f;
            return new PackedEffect
            {
                TypeId = effect.TypeId,
                Position = effect.Position,
                Slot = (byte)effect.Slot,
                Faction = effect.Faction,
                Yaw = (byte)((int)math.round(yaw * 256f) & 255),
                Pitch = (byte)math.round(pitch * 255f),
            };
        }

        public static EffectEvent Unpack(in PackedEffect packed)
        {
            var yaw = packed.Yaw / 256f * 2f * math.PI;
            var pitch = (packed.Pitch / 255f - 0.5f) * math.PI;
            math.sincos(yaw, out var sinYaw, out var cosYaw);
            math.sincos(pitch, out var sinPitch, out var cosPitch);
            return new EffectEvent
            {
                TypeId = packed.TypeId,
                Slot = (EffectSlot)packed.Slot,
                Faction = packed.Faction,
                Position = packed.Position,
                Direction = new float3(cosPitch * sinYaw, sinPitch, cosPitch * cosYaw),
            };
        }

        /// <summary>Fills up to <paramref name="maxBatches"/> RPCs of <see cref="EffectRpc.Capacity"/>; the rest is dropped.</summary>
        public static void Batch(NativeArray<EffectEvent> effects, int maxBatches, NativeList<EffectRpc> batches)
        {
            var count = math.min(effects.Length, maxBatches * EffectRpc.Capacity);
            for (var start = 0; start < count; start += EffectRpc.Capacity)
            {
                var rpc = new EffectRpc();
                var end = math.min(start + EffectRpc.Capacity, count);
                for (var i = start; i < end; i++)
                {
                    rpc.Effects.Add(Pack(effects[i]));
                }

                batches.Add(rpc);
            }
        }
    }
}
