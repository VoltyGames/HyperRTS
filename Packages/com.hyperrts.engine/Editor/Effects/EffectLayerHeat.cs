using UnityEngine;

namespace HyperRTS.Editor.Effects
{
    /// <summary>
    /// Writes a six-way layer's glow into its output: the ramp at the emission channel times the heat over life, times
    /// the emission colour.
    /// </summary>
    public sealed class EffectLayerHeat
    {
        private readonly GraphModel _graph;
        private readonly EffectLayer _layer;
        private readonly Vector2 _at;

        public EffectLayerHeat(GraphModel graph, EffectLayer layer, Vector2 at)
        {
            _graph = graph;
            _layer = layer;
            _at = at;
        }

        public void Write(object output)
        {
            // HDR emission as the colour picker stores it, not turned linear, matched in Play.
            var color = _layer.emissionColor;
            _graph.Slot(output, "EmissionRamp", _layer.Required(_layer.emissionRamp, nameof(EffectLayer.emissionRamp)));
            _graph.Slot(output, "EmissionColor", new Vector3(color.r, color.g, color.b));
            var age = _graph.Operator("AgeOverLifetime", _at + new Vector2(-780f, 200f));
            var heat = HeatAtAge(_layer.heatOverLife, age, 200f);
            if (_layer.minHeatOverLife.length > 0)
            {
                // Each particle keeps one pick between the curves, so tongues differ in heat.
                var pick = _graph.Operator("UnityEditor.VFX.Operator.Lerp", _at + new Vector2(-300f, 200f));
                _graph.Link(HeatAtAge(_layer.minHeatOverLife, age, 300f), pick, "x");
                _graph.Link(heat, pick, "y");
                _graph.Link(_graph.Operator("UnityEditor.VFX.Operator.Random", _at + new Vector2(-520f, 400f)), pick, "s");
                heat = pick;
            }

            _graph.Link(heat, output, "Heat");
        }

        private object HeatAtAge(AnimationCurve curve, object age, float y)
        {
            var heat = _graph.Operator("SampleCurve", _at + new Vector2(-520f, y));
            _graph.Slot(heat, "curve", curve);
            _graph.Link(age, heat, "time");
            return heat;
        }
    }
}
