using System.Collections.Generic;
using HyperRTS.Editor.PlayMode.Cheats;

namespace HyperRTS.Editor.PlayMode.Staging.Steps;

/// <summary>Hurts every live unit of a group by an amount through the damage system.</summary>
internal sealed class DamageStep : StagingStep
{
    public string units;
    public float amount;

    public override IEnumerable<string> Uses => new[] { units };

    public override void Check() => Need(units, "units");

    public override void Run(StagingRun run)
    {
        using var listed = run.LiveUnitArray(units, "damage");
        MatchCheats.Damage(run.World, listed, amount);
    }
}
