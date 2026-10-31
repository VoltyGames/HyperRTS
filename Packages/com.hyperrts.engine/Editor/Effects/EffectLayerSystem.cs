using System;
using UnityEngine;

namespace HyperRTS.Editor.Effects
{
    /// <summary>
    /// Writes one layer as a VFX system: what spawns it, World-space particles that start in rig space and move to
    /// where the spawn came from, their forces, and the output for the layer's kind.
    /// </summary>
    public sealed class EffectLayerSystem
    {
        public const string Includes = "Packages/com.hyperrts.engine/Presentation/Effects/";
        private const float Gravity = 9.81f;

        private readonly GraphModel _graph;
        private readonly EffectLayer _layer;
        private readonly float _x;
        private readonly EffectLayerEmission _emission;

        public EffectLayerSystem(GraphModel graph, EffectLayer layer, float x, EffectGraphInputs inputs)
        {
            _graph = graph;
            _layer = layer;
            _x = x;
            _emission = new EffectLayerEmission(graph, layer, x, inputs);
        }

        /// <summary>Writes the system; a child layer takes its parent's update. Returns this layer's update.</summary>
        public object Write(object parentUpdate)
        {
            if (_layer.capacity <= 0)
            {
                throw new InvalidOperationException($"HyperRTS: layer '{_layer.name}' needs a capacity.");
            }

            if (_layer.Kind == EffectLayerKind.Strip && !_layer.Streams)
            {
                throw new InvalidOperationException($"HyperRTS: strip layer '{_layer.name}' needs a rate, since each stream draws one ribbon.");
            }

            var initialize = _graph.Context("VFXBasicInitialize", new Vector2(_x, 100f));
            _emission.Start(initialize, parentUpdate);
            _graph.Particles(initialize, (uint)_layer.capacity);
            if (_layer.Kind == EffectLayerKind.Strip)
            {
                _graph.Strip(initialize, (uint)_layer.capacity);
            }

            Initialize(initialize);
            var update = _graph.Context("VFXBasicUpdate", new Vector2(_x, 700f));
            _graph.Flow(initialize, update);
            Update(update);
            if (_layer.Kind != EffectLayerKind.Source)
            {
                new EffectLayerOutput(_graph, _layer, new Vector2(_x, 1100f)).Write(update);
            }

            return update;
        }

        private void Initialize(object initialize)
        {
            Pick(initialize, "lifetime", _layer.lifetime);
            Pick(initialize, "size", _layer.size);
            if (_layer.aspect > 0f)
            {
                Pick(initialize, "scaleY", new FloatRange(_layer.aspect, _layer.aspect));
            }

            PickColor(initialize);
            if (_layer.Kind == EffectLayerKind.Debris)
            {
                PickPerAxis(initialize, "angle", new FloatRange(0f, 360f));
                PickPerAxis(initialize, "angularVelocity", _layer.spin);
                PickMesh(initialize);
            }
            else
            {
                PickAngle(initialize, "angle", _layer.rotation);
                PickAngle(initialize, "angularVelocity", _layer.spin);
            }

            PickFrame(initialize);
            // VFX puts the particle at the pivot, so a card standing on it has its pivot at the bottom.
            if (_layer.pivot != 0f)
            {
                var pivot = _graph.Block(initialize, "SetAttribute");
                _graph.Set(pivot, "attribute", "pivot");
                _graph.SlotAt(pivot, 0, new Vector3(0f, -_layer.pivot, 0f));
            }

            Emit(initialize);
            _emission.Place(initialize);
        }

        private void PickFrame(object initialize)
        {
            var flipbook = _layer.flipbook;
            if (flipbook.random && flipbook.randomStart)
            {
                throw new InvalidOperationException($"HyperRTS: layer '{_layer.name}' flipbook is both random and randomStart; pick one.");
            }

            if (flipbook.random)
            {
                Pick(initialize, "texIndex", _layer.RandomFrames());
            }

            if (flipbook.randomStart)
            {
                Pick(initialize, "texIndex", new FloatRange(0f, flipbook.Frames - 0.01f));
            }
        }

        private void Emit(object initialize)
        {
            var emit = _graph.Hlsl(initialize, "Emit From Shape", Includes + "GraphEmitShape.hlsl");
            _graph.Slot(emit, "origin", _layer.origin);
            _graph.Slot(emit, "shape", (float)_layer.Shape);
            _graph.Slot(emit, "radius", _layer.radius);
            _graph.Slot(emit, "spread", _layer.spread);
            _graph.Slot(emit, "speed", new Vector2(_layer.speed.min, _layer.speed.max));
            _graph.Slot(emit, "rim", _layer.rim ? 1f : 0f);
            _graph.Slot(emit, "axis", _layer.ShapeAxis);
            _graph.Slot(emit, "shapeScale", _layer.ShapeScale);
            _graph.Slot(emit, "ground", _layer.groundHeight);
        }

        private void Update(object update)
        {
            Forces(update);
            Wander(update);

            // Children and Shader Graphs read the stored alpha and frame, which an output's curve never writes back.
            if (_layer.KeepsAlpha && _layer.alphaOverLife.Count > 0)
            {
                StoreOverLife(update, "alpha", _layer.AlphaCurve());
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
                _graph.Slot(gravity, "Force", new Vector3(0f, -Gravity * _layer.gravity, 0f));
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

        private void Pick(object context, string attribute, FloatRange range)
        {
            var block = _graph.Block(context, "SetAttribute");
            _graph.Set(block, "attribute", attribute);
            Values(block, range.IsConstant, range.min, range.max);
        }

        private void PickColor(object context)
        {
            var block = _graph.Block(context, "SetAttribute");
            _graph.Set(block, "attribute", "color");
            var min = _layer.color.min.linear;
            var max = _layer.color.max.linear;
            Values(block, min == max, new Vector3(min.r, min.g, min.b), new Vector3(max.r, max.g, max.b));
        }

        // A planar particle turns about the view axis alone.
        private void PickAngle(object context, string attribute, FloatRange range)
        {
            if (range.IsConstant && range.min == 0f)
            {
                return;
            }

            var block = _graph.Block(context, "SetAttribute");
            _graph.Set(block, "attribute", attribute);
            _graph.Set(block, "channels", "Z");
            Values(block, range.IsConstant, range.min, range.max);
        }

        private void PickPerAxis(object context, string attribute, FloatRange range)
        {
            var block = _graph.Block(context, "SetAttribute");
            _graph.Set(block, "attribute", attribute);
            _graph.Set(block, "Random", "PerComponent");
            _graph.Slot(block, "A", Vector3.one * range.min);
            _graph.Slot(block, "B", Vector3.one * range.max);
        }

        // Truncating a uniform pick over 0 to count gives each mesh an even share.
        private void PickMesh(object context)
        {
            if (_layer.meshNames.Count < 2)
            {
                return;
            }

            var block = _graph.Block(context, "SetAttribute");
            _graph.Set(block, "attribute", "meshIndex");
            _graph.Set(block, "Random", "Uniform");
            _graph.Slot(block, "A", 0u);
            _graph.Slot(block, "B", (uint)_layer.meshNames.Count);
        }

        private void Values(object block, bool constant, object min, object max)
        {
            if (constant)
            {
                _graph.SlotAt(block, 0, min);
                return;
            }

            _graph.Set(block, "Random", "Uniform");
            _graph.Slot(block, "A", min);
            _graph.Slot(block, "B", max);
        }
    }
}
