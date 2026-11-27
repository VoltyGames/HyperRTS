using HyperRTS.Editor.Effects.Layer;
using HyperRTS.Simulation.Common;
using UnityEngine;

namespace HyperRTS.Editor.Effects.Graph;

/// <summary>Writes a layer's Update context: its forces, wander, stored life curves and ground bounce.</summary>
public sealed class EffectLayerUpdate(GraphModel graph, EffectLayer layer, float x)
{
    private const string Blocks = EffectLayerParticles.Blocks;

    private readonly EffectLayer _layer = layer;

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
            var loop = graph.Hlsl(update, "Loop Frames", Blocks + "LoopFrames.hlsl");
            graph.Slot(loop, "frames", (float)_layer.flipbook.Frames);
            graph.Slot(loop, "span", _layer.flipbook.to - _layer.flipbook.from);
            LinkDeltaTime(loop, 880f);
        }

        if (_layer.ground)
        {
            var bounce = graph.Hlsl(update, "Bounce On Ground", Blocks + "BounceOnGround.hlsl");
            graph.Slot(bounce, "bounce", _layer.bounce);
            graph.Slot(bounce, "friction", _layer.friction);
        }
    }

    private void Forces(object update)
    {
        if (_layer.gravity != 0f)
        {
            var gravity = graph.Block(update, "Gravity");
            graph.Slot(gravity, "Force", new Vector3(0f, -Gravity.Acceleration * _layer.gravity, 0f));
        }

        if (_layer.drag > 0f)
        {
            var drag = graph.Hlsl(update, "Slow Down", Blocks + "SlowDown.hlsl");
            graph.Slot(drag, "drag", _layer.drag);
            graph.Slot(drag, "bySpeed", _layer.dragBySpeed ? 1f : 0f);
            LinkDeltaTime(drag, 760f);
        }

        if (_layer.drift != Vector3.zero)
        {
            var drift = graph.Hlsl(update, "Drift At Velocity", Blocks + "DriftAtVelocity.hlsl");
            graph.Slot(drift, "drift", _layer.drift);
            LinkDeltaTime(drift, 840f);
        }

        if (_layer.wind != 0f)
        {
            var wind = graph.Hlsl(update, "Drift With Wind", Blocks + "DriftWithWind.hlsl");
            graph.Slot(wind, "share", _layer.wind);
            LinkDeltaTime(wind, 800f);
        }
    }

    private void Wander(object update)
    {
        if (_layer.noise.IsSet)
        {
            var noise = graph.Hlsl(update, "Stir With Noise", Blocks + "StirWithNoise.hlsl");
            graph.Slot(noise, "strength", _layer.noise.strength);
            graph.Slot(noise, "frequency", _layer.noise.frequency);
            graph.Slot(noise, "scroll", _layer.noise.scroll);
            graph.Link(graph.BuiltIn("VfxTotalTime", new Vector2(x - 300f, 920f)), noise, "time");
            LinkDeltaTime(noise, 960f);
        }

        if (_layer.orbit.min != 0f || _layer.orbit.max != 0f)
        {
            var orbit = graph.Hlsl(update, "Orbit Spawn Point", Blocks + "OrbitSpawnPoint.hlsl");
            graph.Slot(orbit, "rate", new Vector2(_layer.orbit.min, _layer.orbit.max));
            LinkDeltaTime(orbit, 1000f);
        }
    }

    private void LinkDeltaTime(object block, float y) =>
        graph.Link(graph.BuiltIn("VfxDeltaTime", new Vector2(x - 300f, y)), block, "deltaTime");

    private void StoreOverLife(object update, string attribute, AnimationCurve curve)
    {
        var block = graph.Block(update, "AttributeFromCurve");
        graph.Set(block, "attribute", attribute);
        graph.SlotAt(block, 0, curve);
    }
}
