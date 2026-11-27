using HyperRTS.Simulation.Common.Combat;

namespace HyperRTS.Presentation.Overlays.Bars;

/// <summary>What one unit's health bar shows: fill, segments, rank pips and squad member pips.</summary>
public readonly struct HealthBarData(in Health health, int rank, float radius, in SquadBarState squad, OverlayStyle style)
{
    public readonly float Fraction = health.Fraction;
    public readonly int Segments = style.Segments(health.Max, squad.Members);
    public readonly int Rank = rank;
    public readonly SquadBarState Squad = squad;

    /// <summary>Width in reference pixels.</summary>
    public readonly float Width = style.BarWidth(radius);
}
