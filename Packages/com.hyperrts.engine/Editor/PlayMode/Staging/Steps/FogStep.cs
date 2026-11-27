using HyperRTS.Editor.PlayMode.Cheats;

namespace HyperRTS.Editor.PlayMode.Staging.Steps;

/// <summary>Switches fog of war.</summary>
internal sealed class FogStep : StagingStep
{
    private readonly bool _enabled;

    public FogStep(bool enabled) => _enabled = enabled;

    public override void Run(StagingRun run) => MatchCheats.SetFog(run.World, _enabled);
}
