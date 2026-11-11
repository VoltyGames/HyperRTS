using HyperRTS.Simulation.Common;

namespace HyperRTS.Presentation.Overlays.Bars
{
    /// <summary>What one unit's health bar shows: fill, segments, rank pips and squad member pips.</summary>
    public readonly struct HealthBarData
    {
        public readonly float Fraction;
        public readonly int Segments;
        public readonly int Rank;
        public readonly int Members;
        public readonly int Alive;
        public readonly SquadBarState Squad;

        /// <summary>Width in reference pixels.</summary>
        public readonly float Width;

        public HealthBarData(in Health health, int rank, float radius, in SquadBarState squad, OverlayStyle style)
        {
            Fraction = health.Fraction;
            Segments = style.Segments(health.Max, squad.Members);
            Rank = rank;
            Members = squad.Members;
            Alive = squad.Alive;
            Squad = squad;
            Width = style.BarWidth(radius);
        }
    }
}
