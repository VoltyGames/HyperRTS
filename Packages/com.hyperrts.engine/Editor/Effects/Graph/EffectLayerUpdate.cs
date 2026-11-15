using HyperRTS.Editor.Effects.Layer;
using HyperRTS.Simulation.Common;
using UnityEngine;

namespace HyperRTS.Editor.Effects.Graph
{
    /// <summary>Writes a layer's Update context: its forces, wander, stored life curves and ground bounce.</summary>
    public sealed class EffectLayerUpdate
    {
        private const string Includes = EffectLayerParticles.Includes;

        private readonly GraphModel _graph;
        private readonly EffectLayer _layer;
        private readonly float _x;

        public EffectLayerUpdate(GraphModel graph, EffectLayer layer, float x)
        {
            _graph = graph;
            _layer = layer;
            _x = x;
        }

        public void Write(object update)
        {
            Forces(update);
            Wander(update);

            // Children and Shader Graphs read the stored alpha and frame, which an output's curve never writes back.
            if (_layer.KeepsAlpha && _layer.alphaOverLife.length > 0)
            {
                StoreOverLife(update, "alpha", _layer.alphaOverLife);
            }

            if (_layer.DrawsShaderGraph && _layer.flipbook.PlaysOverLife)
            {
                StoreOverLife(update, "texIndex", _layer.FrameCurve());
            }

            if (_layer.flipbook.randomStart)
            {
                var loop = _graph.Hlsl(update, "Loop Frames", Includes + "GraphLoopFrames.hlsl");
                _graph.Slot(loop, "frames", (float)_layer.flipbook.Frames);
                _graph.Slot(loop, "span", _layer.flipbook.to - _layer.flipbook.from);
                LinkDeltaTime(loop, 880f);
            }

            if (_layer.ground)
            {
                var bounce = _graph.Hlsl(update, "Bounce On Ground", Includes + "GraphGroundBounce.hlsl");
                _graph.Slot(bounce, "bounce", _layer.bounce);
                _graph.Slot(bounce, "friction", _layer.friction);
            }
        }

        private void Forces(object update)
        {
            if (_layer.gravity != 0f)
            {
                var gravity = _graph.Block(update, "Gravity");
                _graph.Slot(gravity, "Force", new Vector3(0f, -Gravity.Acceleration * _layer.gravity, 0f));
            }

            if (_layer.drag > 0f)
            {
                var drag = _graph.Hlsl(update, "Slow Down", Includes + "GraphDrag.hlsl");
                _graph.Slot(drag, "drag", _layer.drag);
                _graph.Slot(drag, "bySpeed", _layer.dragBySpeed ? 1f : 0f);
                LinkDeltaTime(drag, 760f);
            }

            if (_layer.drift != Vector3.zero)
            {
                var drift = _graph.Hlsl(update, "Drift At Velocity", Includes + "GraphDrift.hlsl");
                _graph.Slot(drift, "drift", _layer.drift);
                LinkDeltaTime(drift, 840f);
            }

            if (_layer.wind != 0f)
            {
                var wind = _graph.Hlsl(update, "Drift With Wind", Includes + "GraphWind.hlsl");
                _graph.Slot(wind, "share", _layer.wind);
                LinkDeltaTime(wind, 800f);
            }
        }

        private void Wander(object update)
        {
            if (_layer.noise.IsSet)
            {
                var noise = _graph.Hlsl(update, "Stir With Noise", Includes + "GraphNoise.hlsl");
                _graph.Slot(noise, "strength", _layer.noise.strength);
                _graph.Slot(noise, "frequency", _layer.noise.frequency);
                _graph.Slot(noise, "scroll", _layer.noise.scroll);
                _graph.Link(_graph.BuiltIn("VfxTotalTime", new Vector2(_x - 300f, 920f)), noise, "time");
                LinkDeltaTime(noise, 960f);
            }

            if (_layer.orbit.min != 0f || _layer.orbit.max != 0f)
            {
                var orbit = _graph.Hlsl(update, "Orbit Spawn Point", Includes + "GraphOrbit.hlsl");
                _graph.Slot(orbit, "rate", new Vector2(_layer.orbit.min, _layer.orbit.max));
                LinkDeltaTime(orbit, 1000f);
            }
        }

        private void LinkDeltaTime(object block, float y) =>
            _graph.Link(_graph.BuiltIn("VfxDeltaTime", new Vector2(_x - 300f, y)), block, "deltaTime");

        private void StoreOverLife(object update, string attribute, AnimationCurve curve)
        {
            var block = _graph.Block(update, "AttributeFromCurve");
            _graph.Set(block, "attribute", attribute);
            _graph.SlotAt(block, 0, curve);
        }
    }
}
