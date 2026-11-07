using HyperRTS.Simulation.Common.Players;
using HyperRTS.Simulation.Cues.Surfaces;
using HyperRTS.Simulation.Vision.Sight;
using System.Runtime.InteropServices;
using HyperRTS.Simulation.Common;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Cues
{
    /// <summary>
    /// One <see cref="CueEvent"/> in 18 bytes, for the network and replays; positions and radii in
    /// <see cref="QuantizedPosition"/> steps.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct PackedCue
    {
        private const float PitchSteps = 127f;
        private const int ConcealmentShift = 4;
        private const byte SurfaceBits = 0x0F;
        private const byte TallBit = 0x40;

        public int TypeId;
        public short X;
        public short Y;
        public short Z;
        public ushort Radius;
        public byte Slot;
        public byte Faction;

        /// <summary>Heading in 256ths of a turn, clockwise from +Z.</summary>
        public byte Yaw;

        /// <summary>Elevation in 7 bits from straight down (0) to straight up (127).</summary>
        public byte Pitch;

        // Bytes rather than the enums, so the RPC codegen sends one byte each.
        /// <summary>The impact's surface in the low 4 bits, the source's concealment in the next 2, then its tall bit.</summary>
        public byte SurfaceConcealment;

        /// <summary>An impact's material, else the cue's <see cref="CueEvent.Variant"/>; impacts never set one.</summary>
        public byte Detail;

        public static PackedCue Pack(in CueEvent cue)
        {
            var direction = math.normalizesafe(cue.Direction, new float3(0f, 0f, 1f));
            var pitch = math.asin(math.clamp(direction.y, -1f, 1f)) / math.PI + 0.5f;
            var tall = cue.Tall ? TallBit : 0;
            return new PackedCue
            {
                TypeId = cue.TypeId,
                X = QuantizedPosition.Quantize(cue.Position.x),
                Y = QuantizedPosition.Quantize(cue.Position.y),
                Z = QuantizedPosition.Quantize(cue.Position.z),
                Radius = (ushort)math.clamp(math.round(cue.Radius / QuantizedPosition.Step), 0, ushort.MaxValue),
                Slot = (byte)cue.Slot,
                Faction = cue.Faction,
                Yaw = HeadingByte.Encode(direction.xz),
                Pitch = (byte)math.round(pitch * PitchSteps),
                SurfaceConcealment = (byte)((int)cue.Surface | (int)cue.Concealment << ConcealmentShift | tall),
                Detail = cue.Slot == CueSlot.Impact ? (byte)cue.Material : cue.Variant,
            };
        }

        public readonly CueEvent Unpack()
        {
            var pitch = (Pitch / PitchSteps - 0.5f) * math.PI;
            var level = HeadingByte.Direction(Yaw);
            math.sincos(pitch, out var sinPitch, out var cosPitch);
            var impact = (CueSlot)Slot == CueSlot.Impact;
            return new CueEvent
            {
                TypeId = TypeId,
                Slot = (CueSlot)Slot,
                Variant = impact ? (byte)0 : Detail,
                Faction = Faction,
                Position = QuantizedPosition.Dequantize(X, Y, Z),
                Direction = new float3(level.x * cosPitch, sinPitch, level.z * cosPitch),
                Concealment = (Concealment)((SurfaceConcealment >> ConcealmentShift) & 3),
                Tall = (SurfaceConcealment & TallBit) != 0,
                Radius = Radius * QuantizedPosition.Step,
                Surface = (ImpactSurface)(SurfaceConcealment & SurfaceBits),
                Material = impact ? (SurfaceMaterial)Detail : default,
            };
        }
    }
}
