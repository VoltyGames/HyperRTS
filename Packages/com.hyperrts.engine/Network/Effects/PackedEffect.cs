using Unity.Mathematics;

namespace HyperRTS.Network.Effects
{
    /// <summary>One <c>EffectEvent</c> on the wire in 20 bytes; fields ordered so the struct needs no padding.</summary>
    public struct PackedEffect
    {
        public int TypeId;
        public float3 Position;
        public byte Slot;
        public byte Faction;

        /// <summary>Heading in 256ths of a turn, clockwise from +Z.</summary>
        public byte Yaw;

        /// <summary>Elevation from straight down (0) to straight up (255).</summary>
        public byte Pitch;
    }
}
