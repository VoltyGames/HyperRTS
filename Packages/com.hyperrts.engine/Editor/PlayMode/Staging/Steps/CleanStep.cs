using HyperRTS.Presentation.Overlays;
using HyperRTS.Presentation.Rendering;
using UnityEngine;
using UnityEngine.UIElements;

namespace HyperRTS.Editor.PlayMode.Staging.Steps
{
    /// <summary>Hides or shows every UI panel, the unit overlays (markers, health bars, rally lines) and the silhouettes of hidden units, for clean footage.</summary>
    internal sealed class CleanStep : StagingStep
    {
        private readonly bool _clean;

        public CleanStep(bool clean) => _clean = clean;

        public override void Run(StagingRun run)
        {
            foreach (var panel in Object.FindObjectsByType<PanelRenderer>())
            {
                panel.enabled = !_clean;
            }

            foreach (var overlays in Object.FindObjectsByType<OverlayRenderer>())
            {
                overlays.enabled = !_clean;
            }

            UnitSilhouettes.Hide(_clean);
        }
    }
}
