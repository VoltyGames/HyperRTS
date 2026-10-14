using HyperRTS.Presentation.Rendering;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace HyperRTS.Editor.Validation.Rules
{
    /// <summary>A match scene needs the height fog feature, with its shader, on every active renderer.</summary>
    public sealed class HeightFogRule : ISceneRule
    {
        private const string FeatureName = "Height Fog";

        public void Check(SceneSet scenes, ValidationIssues issues)
        {
            var hostsMatch = scenes.HostsSubScene;
            if (!hostsMatch)
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
            var feature = RendererFeatures.Find<HeightFogFeature>(renderer);
            if (feature == null)
            {
                issues.Warn(renderer, "No Height Fog feature: the map draws without fog.",
                    "Add Feature", () => AddFeature(renderer));
                return;
            }

            if (feature.shader == null)
            {
                issues.Error(renderer, $"The Height Fog feature has no shader; set it to {HeightFogFeature.ShaderName}.");
            }
        }

        private static void AddFeature(UniversalRendererData renderer)
        {
            var feature = ScriptableObject.CreateInstance<HeightFogFeature>();
            feature.shader = Shader.Find(HeightFogFeature.ShaderName);
            RendererFeatures.Add(renderer, feature, FeatureName);
        }
    }
}
