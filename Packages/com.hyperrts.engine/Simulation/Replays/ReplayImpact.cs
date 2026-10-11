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
            var cell = (int3)math.round(impact.Position / ReplayEntity.PositionStep);
            return new ReplayImpact
            {
                TypeId = impact.TypeId,
                X = (short)math.clamp(cell.x, short.MinValue, short.MaxValue),
                Y = (short)math.clamp(cell.y, short.MinValue, short.MaxValue),
                Z = (short)math.clamp(cell.z, short.MinValue, short.MaxValue),
                Radius = (ushort)math.clamp(math.round(impact.Radius / ReplayEntity.PositionStep), 0, ushort.MaxValue),
                Faction = impact.Faction,
                Surface = impact.Surface,
            };
        }

        public readonly ImpactEvent ToEvent() => new()
        {
            TypeId = TypeId,
            Position = new float3(X, Y, Z) * ReplayEntity.PositionStep,
            Radius = Radius * ReplayEntity.PositionStep,
            Faction = Faction,
            Surface = Surface,
        };
    }
}
