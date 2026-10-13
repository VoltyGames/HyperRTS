using HyperRTS.Presentation.Rendering;
using HyperRTS.Simulation.Units;

namespace HyperRTS.Editor.Validation.Rules
{
    /// <summary>A unit gets a silhouette through cover only if its renderers carry the units rendering layer.</summary>
    public sealed class UnitRenderingLayerRule : AuthoringRule<UnitAuthoring>
    {
        protected override void Check(UnitAuthoring unit, ValidationIssues issues)
        {
            if (UnitRenderingLayer.Missing(unit.gameObject).Length == 0)
            {
                return;
            }

            issues.Warn(unit,
                $"Renderers lack rendering layer {UnitRenderingLayer.Index}: no silhouette shows where cover hides the unit.",
                "Add Layer", () => QuickFixes.AddRenderingLayer(unit.gameObject));
        }
    }
}
