using HyperRTS.Presentation.Rendering;
using HyperRTS.Simulation.Units;

namespace HyperRTS.Editor.Validation.Rules
{
    /// <summary>A unit gets a silhouette through cover only if its renderers carry the silhouette feature's layer.</summary>
    public sealed class UnitRenderingLayerRule : AuthoringRule<UnitAuthoring>
    {
        protected override void Check(UnitAuthoring unit, ValidationIssues issues)
        {
            var feature = UnitSilhouettes.Find();
            if (feature == null)
            {
                return;
            }

            var layer = feature.RenderingLayer;
            if (UnitRenderingLayer.Missing(unit.gameObject, layer).Length == 0)
            {
                return;
            }

            issues.Warn(unit, $"Renderers lack rendering layer {layer}: no silhouette shows where cover hides the unit.",
                "Add Layer", () => QuickFixes.AddRenderingLayer(unit.gameObject, layer));
        }
    }
}
