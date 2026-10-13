using System.Linq;
using HyperRTS.Presentation.Rendering;
using Unity.Scenes;

namespace HyperRTS.Editor.Validation.Rules
{
    /// <summary>A match scene needs the silhouette feature and the named units layer, or hidden units don't show.</summary>
    public sealed class UnitSilhouetteRule : ISceneRule
    {
        public void Check(SceneSet scenes, ValidationIssues issues)
        {
            var hostsMatch = scenes.Roots.Any(root => root.GetComponentInChildren<SubScene>(true) != null);
            if (!hostsMatch)
            {
                return;
            }

            foreach (var renderer in UnitSilhouetteSettings.ActiveRenderers())
            {
                if (!UnitSilhouetteSettings.HasFeature(renderer))
                {
                    issues.Warn(renderer, "No Unit Silhouette feature: units behind cover won't show.",
                        "Add Feature", () => UnitSilhouetteSettings.AddFeature(renderer));
                }
            }

            CheckLayer(issues);
        }

        private static void CheckLayer(ValidationIssues issues)
        {
            var name = UnitSilhouetteSettings.LayerName();
            if (name == UnitRenderingLayer.Name)
            {
                return;
            }

            if (name != string.Empty)
            {
                issues.Error(null,
                    $"Rendering layer {UnitRenderingLayer.Index} is named '{name}', but units use it as '{UnitRenderingLayer.Name}'.");
                return;
            }

            issues.Warn(null, $"Rendering layer {UnitRenderingLayer.Index} isn't named '{UnitRenderingLayer.Name}'.",
                "Name Layer", UnitSilhouetteSettings.NameLayer);
        }
    }
}
