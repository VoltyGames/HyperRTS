using HyperRTS.Presentation.Rendering.Sharpen;

namespace HyperRTS.Editor.Validation.Rules.Effects;

/// <summary>A match scene needs the sharpen feature, with sane settings, on every active renderer.</summary>
public sealed class SharpenRule : RendererFeatureRule<SharpenFeature>
{
    protected override string FeatureName => "Sharpen";

    protected override string MissingMessage =>
        "No Sharpen feature: temporal anti-aliasing leaves ground and leaves soft.";

    protected override string SettingsProblem(SharpenFeature feature) => feature.SettingsProblem();
}
