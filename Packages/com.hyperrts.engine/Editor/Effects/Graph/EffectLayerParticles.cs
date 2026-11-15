using HyperRTS.Editor.Effects.Layer;
using UnityEngine;

namespace HyperRTS.Editor.Effects.Graph
{
    /// <summary>
    /// Writes the Initialize and Update contexts of a layer's particles, and its output: what spawns them, World-space
    /// particles that start in rig space and move to where the spawn came from, their forces, and the output for the
    /// layer's kind.
    /// </summary>
    public sealed class EffectLayerParticles
    {
        public const string Includes = "Packages/com.hyperrts.engine/Presentation/Effects/Shaders/";

        private readonly GraphModel _graph;
        private readonly EffectLayer _layer;
        private readonly float _x;
        private readonly EffectLayerEmission _emission;
        private readonly EffectLayerUpdate _update;
        private readonly AttributePicker _picker;

        public EffectLayerParticles(GraphModel graph, EffectLayer layer, float x, EffectGraphInputs inputs)
        {
            _graph = graph;
            _layer = layer;
            _x = x;
            _emission = new EffectLayerEmission(graph, layer, x, inputs);
            _update = new EffectLayerUpdate(graph, layer, x);
            _picker = new AttributePicker(graph);
        }

        /// <summary>Writes the system; a child layer takes its parent's update. Returns this layer's update.</summary>
        public object Write(object parentUpdate)
        {
            var initialize = _graph.Context("VFXBasicInitialize", new Vector2(_x, 100f));
            _emission.Start(initialize, parentUpdate);
            _graph.Particles(initialize, (uint)_layer.capacity);
            if (_layer.kind == EffectLayerKind.Strip)
            {
                _graph.Strip(initialize, (uint)_layer.capacity);
            }

            Initialize(initialize);
            var update = _graph.Context("VFXBasicUpdate", new Vector2(_x, 700f));
            _graph.Flow(initialize, update);
            _update.Write(update);
            if (_layer.kind != EffectLayerKind.Source)
            {
                new EffectLayerOutput(_graph, _layer, new Vector2(_x, 1100f)).Write(update);
            }

            return update;
        }

        private void Initialize(object initialize)
        {
            _picker.Pick(initialize, "lifetime", _layer.lifetime);
            _picker.Pick(initialize, "size", _layer.size);
            if (_layer.aspect > 0f)
            {
                _picker.PickChannel(initialize, "scale", "Y", new FloatRange(_layer.aspect, _layer.aspect));
            }

            PickColor(initialize);
            if (_layer.kind == EffectLayerKind.Debris)
            {
                _picker.PickPerAxis(initialize, "angle", new FloatRange(0f, 360f));
                _picker.PickPerAxis(initialize, "angularVelocity", _layer.spin);
                PickMesh(initialize);
            }
            else
            {
                _picker.PickAngle(initialize, "angle", _layer.rotation);
                _picker.PickAngle(initialize, "angularVelocity", _layer.spin);
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
            if (flipbook.random)
            {
                _picker.Pick(initialize, "texIndex", _layer.RandomFrames());
            }

            if (flipbook.randomStart)
            {
                _picker.Pick(initialize, "texIndex", new FloatRange(0f, flipbook.Frames - 0.01f));
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

        private void PickColor(object context)
        {
            var block = _graph.Block(context, "SetAttribute");
            _graph.Set(block, "attribute", "color");
            var min = _layer.color.min.linear;
            var max = _layer.color.max.linear;
            _picker.Values(block, min == max, new Vector3(min.r, min.g, min.b), new Vector3(max.r, max.g, max.b));
        }

        // Truncating a uniform pick over 0 to count gives each mesh an even share.
        private void PickMesh(object context)
        {
            if (_layer.debris.meshes.Count < 2)
            {
                return;
            }

            var block = _graph.Block(context, "SetAttribute");
            _graph.Set(block, "attribute", "meshIndex");
            _graph.Set(block, "Random", "Uniform");
            _graph.Slot(block, "A", 0u);
            _graph.Slot(block, "B", (uint)_layer.debris.meshes.Count);
        }
    }
}
