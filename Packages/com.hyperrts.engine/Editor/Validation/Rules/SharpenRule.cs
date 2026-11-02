using HyperRTS.Presentation.Rendering;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace HyperRTS.Editor.Validation.Rules
{
    /// <summary>A match scene needs the sharpen feature, with its shader and sane settings, on every active renderer.</summary>
    public sealed class SharpenRule : ISceneRule
    {
        private const string FeatureName = "Sharpen";

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
            var feature = RendererFeatures.Find<SharpenFeature>(renderer);
            if (feature == null)
            {
                issues.Warn(renderer, "No Sharpen feature: temporal anti-aliasing leaves ground and leaves soft.",
                    "Add Feature", () => AddFeature(renderer));
                return;
            }

            if (feature.shader == null)
            {
                issues.Error(renderer, $"The Sharpen feature has no shader; set it to {SharpenFeature.ShaderName}.");
            }

            var problem = feature.SettingsProblem();
            if (problem.Length > 0)
            {
                issues.Error(renderer, problem);
            }
        }

        private static void AddFeature(UniversalRendererData renderer)
        {
            var feature = ScriptableObject.CreateInstance<SharpenFeature>();
            feature.shader = Shader.Find(SharpenFeature.ShaderName);
            RendererFeatures.Add(renderer, feature, FeatureName);
        }
    }
}
