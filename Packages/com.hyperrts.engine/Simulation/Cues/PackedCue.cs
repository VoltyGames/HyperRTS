using System.IO;
using HyperRTS.Simulation.Common;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Cues
{
    /// <summary>
    /// One <see cref="CueEvent"/> in 18 bytes, for the network and replays; positions and radii in
    /// <see cref="QuantizedPosition"/> steps.
    /// </summary>
    public struct PackedCue
    {
        public const byte StealthedBit = 0x80;
        private const float PitchSteps = 127f;

        public int TypeId;
        public short X;
        public short Y;
        public short Z;
        public ushort Radius;
        public byte Slot;
        public byte Faction;

        /// <summary>Heading in 256ths of a turn, clockwise from +Z.</summary>
        public byte Yaw;

        /// <summary>Elevation in 7 bits from straight down (0) to straight up (127); <see cref="StealthedBit"/> on top.</summary>
        public byte PitchFlags;

        // Bytes rather than the enums, so the RPC codegen sends one byte each.
        public byte Surface;

        /// <summary>An impact's material, else the cue's <see cref="CueEvent.Variant"/>; impacts never set one.</summary>
        public byte Detail;

        public static PackedCue Pack(in CueEvent cue)
        {
            var direction = math.normalizesafe(cue.Direction, new float3(0f, 0f, 1f));
            var pitch = math.asin(math.clamp(direction.y, -1f, 1f)) / math.PI + 0.5f;
            var flags = cue.Stealthed ? StealthedBit : (byte)0;
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
                PitchFlags = (byte)(flags | (int)math.round(pitch * PitchSteps)),
                Surface = (byte)cue.Surface,
                Detail = cue.Slot == CueSlot.Impact ? (byte)cue.Material : cue.Variant,
            };
        }

        public readonly CueEvent Unpack()
        {
            var pitchStep = PitchFlags & ~StealthedBit;
            var pitch = (pitchStep / PitchSteps - 0.5f) * math.PI;
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
                Stealthed = (PitchFlags & StealthedBit) != 0,
                Radius = Radius * QuantizedPosition.Step,
                Surface = (ImpactSurface)Surface,
                Material = impact ? (SurfaceMaterial)Detail : default,
            };
        }

        public readonly void Write(BinaryWriter writer)
        {
            writer.Write(TypeId);
            writer.Write(X);
            writer.Write(Y);
            writer.Write(Z);
            writer.Write(Radius);
            writer.Write(Slot);
            writer.Write(Faction);
            writer.Write(Yaw);
            writer.Write(PitchFlags);
            writer.Write(Surface);
            writer.Write(Detail);
        }

        public static PackedCue Read(BinaryReader reader) => new()
        {
            TypeId = reader.ReadInt32(),
            X = reader.ReadInt16(),
            Y = reader.ReadInt16(),
            Z = reader.ReadInt16(),
            Radius = reader.ReadUInt16(),
            Slot = reader.ReadByte(),
            Faction = reader.ReadByte(),
            Yaw = reader.ReadByte(),
            PitchFlags = reader.ReadByte(),
            Surface = reader.ReadByte(),
            Detail = reader.ReadByte(),
        };
    }
}
