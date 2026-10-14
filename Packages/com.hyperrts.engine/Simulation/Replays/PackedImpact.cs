using System.IO;
using HyperRTS.Simulation.Combat;
using HyperRTS.Simulation.Effects;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Replays
{
    /// <summary>
    /// One <see cref="ImpactEvent"/> in 15 bytes, for replays and the network; positions are quantized like
    /// <see cref="ReplayEntity"/>'s.
    /// </summary>
    public struct PackedImpact
    {
        public int TypeId;
        public short X;
        public short Y;
        public short Z;

        /// <summary>Splash radius in <see cref="ReplayEntity.PositionStep"/>s.</summary>
        public ushort Radius;

        public byte Faction;

        // Bytes rather than the enums, so the RPC codegen sends one byte each.
        public byte Surface;
        public byte Material;

        public static PackedImpact Pack(in ImpactEvent impact) => new()
        {
            TypeId = impact.TypeId,
            X = ReplayEntity.Quantize(impact.Position.x),
            Y = ReplayEntity.Quantize(impact.Position.y),
            Z = ReplayEntity.Quantize(impact.Position.z),
            Radius = (ushort)math.clamp(math.round(impact.Radius / ReplayEntity.PositionStep), 0, ushort.MaxValue),
            Faction = impact.Faction,
            Surface = (byte)impact.Surface,
            Material = (byte)impact.Material,
        };

        public readonly ImpactEvent Unpack() => new()
        {
            TypeId = TypeId,
            Position = ReplayEntity.Dequantize(X, Y, Z),
            Radius = Radius * ReplayEntity.PositionStep,
            Faction = Faction,
            Surface = (ImpactSurface)Surface,
            Material = (SurfaceMaterial)Material,
        };

        public readonly void Write(BinaryWriter writer)
        {
            writer.Write(TypeId);
            writer.Write(X);
            writer.Write(Y);
            writer.Write(Z);
            writer.Write(Radius);
            writer.Write(Faction);
            writer.Write(Surface);
            writer.Write(Material);
        }

        public static PackedImpact Read(BinaryReader reader) => new()
        {
            TypeId = reader.ReadInt32(),
            X = reader.ReadInt16(),
            Y = reader.ReadInt16(),
            Z = reader.ReadInt16(),
            Radius = reader.ReadUInt16(),
            Faction = reader.ReadByte(),
            Surface = reader.ReadByte(),
            Material = reader.ReadByte(),
        };
    }
}
