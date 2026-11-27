using System.Collections.Generic;
using HyperRTS.Editor.PlayMode.Cheats;

namespace HyperRTS.Editor.PlayMode.Staging.Steps;

/// <summary>Ends a group's ability cooldowns, so a power that charges for minutes fires at once.</summary>
internal sealed class ReadyStep(string units) : StagingStep
{
    public override IEnumerable<string> Uses => new[] { units };

    public override void Run(StagingRun run)
    {
        using var listed = run.LiveUnitArray(units, "ready");
        MatchCheats.ReadyAbilities(run.World, listed);
    }
}
