using System.Linq;
using HyperRTS.Presentation.Rendering;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace HyperRTS.Editor.Validation.Rules
{
    /// <summary>A match scene needs contact shadows, with their shader and URP's Screen Space Shadows, on every active renderer.</summary>
    public sealed class ContactShadowsRule : ISceneRule
    {
        private const string FeatureName = "Contact Shadows";

        // URP keeps the Screen Space Shadows feature internal.
        private const string ScreenShadowsType = "ScreenSpaceShadows";

        public void Check(SceneSet scenes, ValidationIssues issues)
        {
            if (!scenes.HostsSubScene)
            {
                return;
            }

            foreach (var renderer in RendererFeatures.ActiveRenderers())
            {
                CheckRenderer(renderer, issues);
            }
        }

        private static void CheckRenderer(UniversalRendererData renderer, ValidationIssues issues)
        {
            var feature = RendererFeatures.Find<ContactShadowsFeature>(renderer);
            if (feature == null)
            {
                issues.Warn(renderer, "No Contact Shadows feature: small things lose their shadows at the ground.",
                    "Add Feature", () => AddFeature(renderer));
                return;
            }

            if (feature.shader == null)
            {
                issues.Error(renderer, $"The Contact Shadows feature has no shader; set it to {ContactShadowsFeature.ShaderName}.");
            }

            var problem = feature.SettingsProblem();
            if (problem.Length > 0)
            {
                issues.Error(renderer, problem);
            }

            if (renderer.renderingMode is RenderingMode.Deferred or RenderingMode.DeferredPlus)
            {
                issues.Error(renderer, "Contact shadows run after the depth prepass, so the renderer must be Forward or Forward+.");
            }

            if (!renderer.rendererFeatures.Any(f => f != null && f.GetType().Name == ScreenShadowsType))
            {
                issues.Error(renderer, "Contact shadows darken URP's screen-space shadows: add its Screen Space Shadows feature.");
            }
        }

        private static void AddFeature(UniversalRendererData renderer)
        {
            var feature = ScriptableObject.CreateInstance<ContactShadowsFeature>();
            feature.shader = Shader.Find(ContactShadowsFeature.ShaderName);
            RendererFeatures.Add(renderer, feature, FeatureName);
        }
    }
}
