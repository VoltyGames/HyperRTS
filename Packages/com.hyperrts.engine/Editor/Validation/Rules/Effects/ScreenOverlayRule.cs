using HyperRTS.Editor.Validation.Issues;
using HyperRTS.Editor.Validation.Project;
using HyperRTS.Editor.Validation.Sets;
using HyperRTS.Presentation.Rendering.ScreenOverlay;
using UnityEngine;

namespace HyperRTS.Editor.Validation.Rules.Effects
{
    /// <summary>A match scene needs the screen overlay feature on every active renderer, or no health bar draws.</summary>
    public sealed class ScreenOverlayRule : ISceneRule
    {
        private const string FeatureName = "Screen Overlay";

        public void Check(SceneSet scenes, ValidationIssues issues)
        {
            if (!scenes.HostsSubScene)
            {
                return;
            }

            foreach (var renderer in RendererFeatures.ActiveRenderers())
            {
                if (RendererFeatures.Find<ScreenOverlayFeature>(renderer) == null)
                {
                    issues.Error(renderer, "No Screen Overlay feature: health bars never draw.", "Add Feature", () =>
                        RendererFeatures.Add(renderer, ScriptableObject.CreateInstance<ScreenOverlayFeature>(), FeatureName));
                }
            }
        }
    }
}
