using HyperRTS.Presentation.Rendering.TeamOutline;

namespace HyperRTS.Editor.Validation.Rules.Effects
{
    /// <summary>A match scene needs the team outline feature, with sane settings, on every active renderer.</summary>
    public sealed class TeamOutlineRule : RendererFeatureRule<TeamOutlineFeature>
    {
        protected override string FeatureName => "Team Outline";

        protected override string MissingMessage =>
            "No Team Outline feature: units and buildings show no line in their owner's colour.";

        protected override string SettingsProblem(TeamOutlineFeature feature) => feature.SettingsProblem();
    }
}
