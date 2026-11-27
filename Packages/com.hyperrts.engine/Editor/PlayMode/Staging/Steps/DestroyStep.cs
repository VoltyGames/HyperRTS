using System.Collections.Generic;
using HyperRTS.Editor.PlayMode.Cheats;

namespace HyperRTS.Editor.PlayMode.Staging.Steps;

/// <summary>Kills a group through the damage system, so death effects play as in a fight.</summary>
internal sealed class DestroyStep(string units) : StagingStep
{
    public override IEnumerable<string> Uses => new[] { units };

    public override void Run(StagingRun run)
    {
        using var listed = run.LiveUnitArray(units, "destroy");
        MatchCheats.Destroy(run.World, listed);
    }
}
