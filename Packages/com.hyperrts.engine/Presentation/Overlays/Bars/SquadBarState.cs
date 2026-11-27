using HyperRTS.Simulation.Common.Infantry;

namespace HyperRTS.Presentation.Overlays.Bars;

/// <summary>What a squad's health bar adds: member pips, the best cover its living members hold, suppression, retreating.</summary>
public readonly struct SquadBarState(float suppression, SuppressionLevel level, CoverKind cover, bool retreating, int members,
    int alive)
{
    /// <summary>Suppression from 0 to 1, or -1 for anything but a squad.</summary>
    public readonly float Suppression = suppression;
    public readonly SuppressionLevel Level = level;
    public readonly CoverKind Cover = cover;
    public readonly bool Retreating = retreating;

    /// <summary>Zero for anything but a squad.</summary>
    public readonly int Members = members;
    public readonly int Alive = alive;

    public static SquadBarState None => new(-1f, SuppressionLevel.Steady, CoverKind.None, false, 0, 0);

    public static SquadBarState Read(in OverlayUnit unit, in SquadRules rules)
    {
        if (!unit.IsSquad || !unit.Suppression.HasValue)
        {
            return None;
        }

        var cover = CoverKind.None;
        var alive = 0;
        foreach (var member in unit.Members)
        {
            alive += member.Alive ? 1 : 0;
            cover = member.Alive && member.Cover > cover ? member.Cover : cover;
        }

        var suppression = unit.Suppression.Value;
        return new SquadBarState(suppression.Value, rules.Level(suppression.Value), cover, unit.Retreating,
            unit.MemberCount, alive);
    }
}
