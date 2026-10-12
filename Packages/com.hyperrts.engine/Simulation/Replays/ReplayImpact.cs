using HyperRTS.Simulation.Combat;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Replays
{
    /// <summary>One recorded <see cref="ImpactEvent"/>, quantized like <see cref="ReplayEntity"/> positions.</summary>
    public struct ReplayImpact
    {
        public int TypeId;
        public short X;
        public short Y;
        public short Z;

        /// <summary>Splash radius in <see cref="ReplayEntity.PositionStep"/>s.</summary>
        public ushort Radius;

        public byte Faction;
        public ImpactSurface Surface;

        public static ReplayImpact Capture(in ImpactEvent impact)
        {
            return new ReplayImpact
            {
                TypeId = impact.TypeId,
                X = ReplayEntity.Quantize(impact.Position.x),
                Y = ReplayEntity.Quantize(impact.Position.y),
                Z = ReplayEntity.Quantize(impact.Position.z),
                Radius = (ushort)math.clamp(math.round(impact.Radius / ReplayEntity.PositionStep), 0, ushort.MaxValue),
                Faction = impact.Faction,
                Surface = impact.Surface,
            };
        }

        public readonly ImpactEvent ToEvent() => new()
        {
            TypeId = TypeId,
            Position = ReplayEntity.Dequantize(X, Y, Z),
            Radius = Radius * ReplayEntity.PositionStep,
            Faction = Faction,
            Surface = Surface,
        };
    }
}
