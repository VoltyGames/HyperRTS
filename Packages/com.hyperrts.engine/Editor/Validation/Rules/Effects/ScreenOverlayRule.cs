using HyperRTS.Presentation.Rendering.ScreenOverlay;

namespace HyperRTS.Editor.Validation.Rules.Effects;

/// <summary>A match scene needs the screen overlay feature on every active renderer, or no health bar draws.</summary>
public sealed class ScreenOverlayRule : RendererFeatureRule<ScreenOverlayFeature>
{
    protected override string FeatureName => "Screen Overlay";

    protected override string MissingMessage => "No Screen Overlay feature: health bars never draw.";

    protected override bool MissingIsError => true;
}
