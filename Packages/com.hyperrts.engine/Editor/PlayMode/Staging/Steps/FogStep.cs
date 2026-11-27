using HyperRTS.Editor.PlayMode.Cheats;

namespace HyperRTS.Editor.PlayMode.Staging.Steps;

/// <summary>Switches fog of war.</summary>
internal sealed class FogStep(bool enabled) : StagingStep
{
    public override void Run(StagingRun run) => MatchCheats.SetFog(run.World, enabled);
}
