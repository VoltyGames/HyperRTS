using Unity.Mathematics;

namespace HyperRTS.Network.Cues
{
    /// <summary>One <c>CueEvent</c> on the wire in 20 bytes; fields ordered so the struct needs no padding.</summary>
    public struct PackedCue
    {
        public const byte StealthedBit = 0x80;

        public int TypeId;
        public float3 Position;
        public byte Slot;
        public byte Faction;

        /// <summary>Heading in 256ths of a turn, clockwise from +Z.</summary>
        public byte Yaw;

        /// <summary>Elevation in 7 bits from straight down (0) to straight up (127); <see cref="StealthedBit"/> on top.</summary>
        public byte PitchFlags;
    }
}
