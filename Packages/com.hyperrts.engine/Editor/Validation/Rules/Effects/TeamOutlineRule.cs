using HyperRTS.Editor.Validation.Issues;
using HyperRTS.Editor.Validation.Project;
using HyperRTS.Editor.Validation.Sets;
using HyperRTS.Presentation.Rendering.TeamOutline;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace HyperRTS.Editor.Validation.Rules.Effects
{
    /// <summary>A match scene needs the team outline feature, with sane settings, on every active renderer.</summary>
    public sealed class TeamOutlineRule : ISceneRule
    {
        private const string FeatureName = "Team Outline";

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
            var feature = RendererFeatures.Find<TeamOutlineFeature>(renderer);
            if (feature == null)
            {
                issues.Warn(renderer, "No Team Outline feature: units and buildings show no line in their owner's colour.",
                    "Add Feature", () =>
                        RendererFeatures.Add(renderer, ScriptableObject.CreateInstance<TeamOutlineFeature>(), FeatureName));
                return;
            }

            var problem = feature.SettingsProblem();
            if (problem.Length > 0)
            {
                issues.Error(renderer, problem);
            }
        }
    }
}
