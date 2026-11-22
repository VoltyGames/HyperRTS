using System.Linq;
using HyperRTS.Editor.Validation.Issues;
using HyperRTS.Presentation.Rendering.ContactShadows;
using UnityEngine.Rendering.Universal;

namespace HyperRTS.Editor.Validation.Rules.Effects
{
    /// <summary>A match scene needs contact shadows, with URP's Screen Space Shadows, on every active renderer.</summary>
    public sealed class ContactShadowsRule : RendererFeatureRule<ContactShadowsFeature>
    {
        // URP keeps the Screen Space Shadows feature internal.
        private const string ScreenShadowsType = "ScreenSpaceShadows";

        protected override string FeatureName => "Contact Shadows";

        protected override string MissingMessage => "No Contact Shadows feature: small things lose their shadows at the ground.";

        protected override string SettingsProblem(ContactShadowsFeature feature) => feature.SettingsProblem();

        protected override void CheckRenderer(UniversalRendererData renderer, ContactShadowsFeature feature, ValidationIssues issues)
        {
            base.CheckRenderer(renderer, feature, issues);
            if (renderer.renderingMode is RenderingMode.Deferred or RenderingMode.DeferredPlus)
            {
                issues.Error(renderer, "Contact shadows run after the depth prepass, so the renderer must be Forward or Forward+.");
            }

            if (!renderer.rendererFeatures.Any(f => f != null && f.GetType().Name == ScreenShadowsType))
            {
                issues.Error(renderer, "Contact shadows darken URP's screen-space shadows: add its Screen Space Shadows feature.");
            }
        }
    }
}
