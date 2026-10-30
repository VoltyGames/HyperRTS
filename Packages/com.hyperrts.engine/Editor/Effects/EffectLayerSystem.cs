using System;
using UnityEngine;

namespace HyperRTS.Editor.Effects
{
    /// <summary>
    /// Writes one layer as a VFX system: a spawner whose bursts Density thins, World-space particles that start in
    /// rig space and move to the burst pose, their forces, and the output for the layer's kind.
    /// </summary>
    public sealed class EffectLayerSystem
    {
        public const string Includes = "Packages/com.hyperrts.engine/Presentation/Effects/";
        private const float Gravity = 9.81f;

        private readonly GraphModel _graph;
        private readonly EffectLayer _layer;
        private readonly float _x;
        private readonly object _density;
        private readonly object[] _pose;

        public EffectLayerSystem(GraphModel graph, EffectLayer layer, float x, object density, object[] pose)
        {
            _graph = graph;
            _layer = layer;
            _x = x;
            _density = density;
            _pose = pose;
        }

        public void Write(object burstEvent)
        {
            if (_layer.capacity <= 0 || _layer.bursts.Count == 0)
            {
                throw new InvalidOperationException($"HyperRTS: layer '{_layer.name}' needs a capacity and at least one burst.");
            }

            var spawner = Spawner();
            _graph.Flow(burstEvent, spawner);
            var initialize = _graph.Context("VFXBasicInitialize", new Vector2(_x, 100f));
            _graph.Flow(spawner, initialize);
            _graph.Particles(initialize, (uint)_layer.capacity);
            Initialize(initialize);
            var update = _graph.Context("VFXBasicUpdate", new Vector2(_x, 700f));
            _graph.Flow(initialize, update);
            Update(update);
            new EffectLayerOutput(_graph, _layer, new Vector2(_x, 1100f)).Write(update);
        }

        private object Spawner()
        {
            var spawner = _graph.Context("VFXBasicSpawner", new Vector2(_x, -200f));
            _graph.Label(spawner, _layer.name);
            for (var i = 0; i < _layer.bursts.Count; i++)
            {
                var burst = _layer.bursts[i];
                var block = _graph.Block(spawner, "VFXSpawnerBurst");
                var times = _graph.Operator("Multiply", new Vector2(_x - 260f, -150f + 90f * i));
                if (burst.count.IsConstant)
                {
                    _graph.SlotAt(times, 0, burst.count.min);
                }
                else
                {
                    _graph.Set(block, "spawnMode", "Random");
                    _graph.OperandType(times, 0, typeof(Vector2));
                    _graph.OperandType(times, 1, typeof(Vector2));
                    _graph.SlotAt(times, 0, new Vector2(burst.count.min, burst.count.max));
                }

                _graph.Slot(block, "Delay", burst.delay);
                _graph.Link(_density, times, "b");
                _graph.Link(times, block, "Count");
            }

            return spawner;
        }

        private void Initialize(object initialize)
        {
            Pick(initialize, "lifetime", _layer.lifetime);
            Pick(initialize, "size", _layer.size);
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

            if (_layer.flipbook.random)
            {
                Pick(initialize, "texIndex", new FloatRange(0f, _layer.flipbook.Frames - 0.01f));
            }

            var emit = _graph.Hlsl(initialize, "Emit From Shape", Includes + "GraphEmitShape.hlsl");
            _graph.Slot(emit, "origin", _layer.origin);
            _graph.Slot(emit, "shape", (float)_layer.Shape);
            _graph.Slot(emit, "radius", _layer.radius);
            _graph.Slot(emit, "spread", _layer.spread);
            _graph.Slot(emit, "speed", new Vector2(_layer.speed.min, _layer.speed.max));
            var place = _graph.Hlsl(initialize, "Place At Burst", Includes + "GraphBurstPose.hlsl");
            var inputs = new[] { "origin", "normal", "forward", "scale" };
            for (var i = 0; i < inputs.Length; i++)
            {
                _graph.Link(_pose[i], place, inputs[i]);
            }
        }

        private void Update(object update)
        {
            if (_layer.gravity != 0f)
            {
                var gravity = _graph.Block(update, "Gravity");
                _graph.Slot(gravity, "Force", new Vector3(0f, -Gravity * _layer.gravity, 0f));
            }

            if (_layer.drag > 0f)
            {
                var drag = _graph.Block(update, "Drag");
                _graph.Slot(drag, "dragCoefficient", _layer.drag);
            }

            if (_layer.Kind == EffectLayerKind.Debris)
            {
                var bounce = _graph.Hlsl(update, "Bounce On Ground", Includes + "GraphGroundBounce.hlsl");
                _graph.Slot(bounce, "bounce", _layer.bounce);
                _graph.Slot(bounce, "friction", _layer.friction);
            }
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
