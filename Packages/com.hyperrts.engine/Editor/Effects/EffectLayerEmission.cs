using System;
using UnityEngine;

namespace HyperRTS.Editor.Effects
{
    /// <summary>
    /// What starts a layer's particles and where they are placed: bursts on the Burst event at its pose, a rate while
    /// a stream plays along the stream's path, or a GPU event from a parent layer's particles at their position.
    /// </summary>
    public sealed class EffectLayerEmission
    {
        private readonly GraphModel _graph;
        private readonly EffectLayer _layer;
        private readonly float _x;
        private readonly EffectGraphInputs _inputs;

        public EffectLayerEmission(GraphModel graph, EffectLayer layer, float x, EffectGraphInputs inputs)
        {
            _graph = graph;
            _layer = layer;
            _x = x;
            _inputs = inputs;
        }

        /// <summary>Links what spawns the layer into its Initialize; a child takes the update of its parent layer.</summary>
        public void Start(object initialize, object parentUpdate)
        {
            if (_layer.HasParent)
            {
                _graph.Flow(Trigger(parentUpdate), initialize);
                return;
            }

            _graph.Flow(_layer.Streams ? RateSpawner() : BurstSpawner(), initialize);
        }

        /// <summary>Moves particles from rig space to where their spawn came from; ends Initialize.</summary>
        public void Place(object initialize)
        {
            if (_layer.HasParent)
            {
                PlaceAtParent(initialize);
                return;
            }

            var streams = _layer.Streams;
            var block = streams
                ? _graph.Hlsl(initialize, "Place Along Path", EffectLayerSystem.Includes + "GraphStreamPose.hlsl")
                : _graph.Hlsl(initialize, "Place At Burst", EffectLayerSystem.Includes + "GraphBurstPose.hlsl");
            var inputs = streams ? new[] { "from", "to", "normal", "forward", "scale" } : new[] { "origin", "normal", "forward", "scale" };
            var sources = streams ? _inputs.StreamPose : _inputs.BurstPose;
            for (var i = 0; i < inputs.Length; i++)
            {
                _graph.Link(sources[i], block, inputs[i]);
            }
        }

        private object BurstSpawner()
        {
            if (_layer.bursts.Count == 0)
            {
                throw new InvalidOperationException($"HyperRTS: layer '{_layer.name}' needs bursts, a rate or a parent.");
            }

            var spawner = Spawner();
            _graph.Flow(_inputs.Burst, spawner);
            var row = 0;
            foreach (var burst in _layer.bursts)
            {
                for (var repeat = 0; repeat <= burst.repeats; repeat++)
                {
                    Burst(spawner, burst, burst.delay + repeat * burst.interval, row++);
                }
            }

            return spawner;
        }

        private void Burst(object spawner, EffectBurst burst, float delay, int row)
        {
            var block = _graph.Block(spawner, "VFXSpawnerBurst");
            var times = _graph.Operator("Multiply", new Vector2(_x - 260f, -150f + 90f * row));
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

            _graph.Slot(block, "Delay", delay);
            _graph.Link(_inputs.Density, times, "b");
            _graph.Link(times, block, "Count");
        }

        // Particles a second: the rate plus the rate per metre times the stream's speed, thinned by Density.
        private object RateSpawner()
        {
            if (_layer.bursts.Count > 0)
            {
                throw new InvalidOperationException($"HyperRTS: layer '{_layer.name}' has both bursts and a rate; split it in two.");
            }

            var spawner = Spawner();
            _graph.Flow(_inputs.Play, spawner);
            _graph.Flow(_inputs.Stop, spawner, 1);
            var perMetre = _graph.Operator("Multiply", new Vector2(_x - 780f, -150f));
            _graph.SlotAt(perMetre, 0, _layer.ratePerMetre);
            _graph.Link(_inputs.Speed, perMetre, "b");
            var total = _graph.Operator("Add", new Vector2(_x - 520f, -150f));
            _graph.SlotAt(total, 0, _layer.rate);
            _graph.Link(perMetre, total, "b");
            var thinned = _graph.Operator("Multiply", new Vector2(_x - 260f, -150f));
            _graph.Link(total, thinned, "a");
            _graph.Link(_inputs.Density, thinned, "b");
            _graph.Link(thinned, _graph.Block(spawner, "VFXSpawnerConstantRate"), "Rate");
            return spawner;
        }

        // Effect detail thins a child only through its parent's count, as Shuriken sub-emitters do.
        private object Trigger(object parentUpdate)
        {
            if (_layer.rate > 0f && _layer.ratePerMetre == 0f && _layer.bursts.Count == 0)
            {
                var trigger = _graph.Block(parentUpdate, "TriggerEvent");
                _graph.Set(trigger, "mode", "OverTime");
                _graph.Slot(trigger, "Rate", _layer.rate);
                var gpuEvent = _graph.Context("VFXBasicGPUEvent", new Vector2(_x, -200f));
                _graph.Link(trigger, gpuEvent, "evt");
                return gpuEvent;
            }

            throw new InvalidOperationException($"HyperRTS: child layer '{_layer.name}' needs a rate a second per parent particle, and no bursts or rate per metre.");
        }

        // A child rises from its parent upright, scaled by the parent's size and faded by its alpha.
        private void PlaceAtParent(object initialize)
        {
            var place = _graph.Hlsl(initialize, "Place At Parent", EffectLayerSystem.Includes + "GraphBurstPose.hlsl");
            _graph.Link(_graph.SourceAttribute("position", new Vector2(_x - 300f, 300f)), place, "origin");
            _graph.Slot(place, "normal", Vector3.up);
            _graph.Slot(place, "forward", Vector3.forward);
            _graph.Link(_graph.SourceAttribute("size", new Vector2(_x - 300f, 420f)), place, "scale");
            var fade = _graph.Block(initialize, "SetAttribute");
            _graph.Set(fade, "attribute", "alpha");
            _graph.Set(fade, "Composition", "Multiply");
            _graph.Link(_graph.SourceAttribute("alpha", new Vector2(_x - 300f, 540f)), fade, "Alpha");
        }

        private object Spawner()
        {
            var spawner = _graph.Context("VFXBasicSpawner", new Vector2(_x, -200f));
            _graph.Label(spawner, _layer.name);
            return spawner;
        }
    }
}
