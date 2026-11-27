using HyperRTS.Editor.Effects.Layer;
using UnityEngine;

namespace HyperRTS.Editor.Effects.Graph;

/// <summary>
/// What starts a layer's particles and where they are placed: bursts on the Burst event at its pose, a rate while
/// a stream plays along the stream's path, or a GPU event from a parent layer's particles at their position.
/// </summary>
public sealed class EffectLayerEmission(GraphModel graph, EffectLayer layer, float x, EffectGraphInputs inputs)
{
    private readonly EffectLayer _layer = layer;
    private readonly EffectGraphInputs _inputs = inputs;

    /// <summary>Links what spawns the layer into its Initialize; a child takes the update of its parent layer.</summary>
    public void Start(object initialize, object parentUpdate)
    {
        if (_layer.HasParent)
        {
            graph.Flow(Trigger(parentUpdate), initialize);
            return;
        }

        graph.Flow(_layer.Streams ? RateSpawner() : BurstSpawner(), initialize);
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
            ? graph.Hlsl(initialize, "Place Along Path", EffectLayerParticles.Blocks + "PlaceAlongPath.hlsl")
            : graph.Hlsl(initialize, "Place At Burst", EffectLayerParticles.Blocks + "PlaceAtBurst.hlsl");
        var inputs = streams ? new[] { "from", "to", "normal", "forward", "scale" } : ["origin", "normal", "forward", "scale"];
        var sources = streams ? _inputs.StreamPose : _inputs.BurstPose;
        for (var i = 0; i < inputs.Length; i++)
        {
            graph.Link(sources[i], block, inputs[i]);
        }
    }

    private object BurstSpawner()
    {
        var spawner = Spawner();
        graph.Flow(_inputs.Burst, spawner);
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
        var block = graph.Block(spawner, "VFXSpawnerBurst");
        var times = graph.Operator("Multiply", new Vector2(x - 260f, -150f + 90f * row));
        if (burst.count.IsConstant)
        {
            graph.SlotAt(times, 0, burst.count.min);
        }
        else
        {
            graph.Set(block, "spawnMode", "Random");
            graph.OperandType(times, 0, typeof(Vector2));
            graph.OperandType(times, 1, typeof(Vector2));
            graph.SlotAt(times, 0, new Vector2(burst.count.min, burst.count.max));
        }

        graph.Slot(block, "Delay", delay);
        graph.Link(_inputs.Density, times, "b");
        graph.Link(times, block, "Count");
    }

    // Particles a second: the rate plus the rate per metre times the stream's speed, thinned by Density.
    private object RateSpawner()
    {
        var spawner = Spawner();
        graph.Flow(_inputs.Play, spawner);
        graph.Flow(_inputs.Stop, spawner, 1);
        var perMetre = graph.Operator("Multiply", new Vector2(x - 780f, -150f));
        graph.SlotAt(perMetre, 0, _layer.ratePerMetre);
        graph.Link(_inputs.Speed, perMetre, "b");
        var total = graph.Operator("Add", new Vector2(x - 520f, -150f));
        graph.SlotAt(total, 0, _layer.rate);
        graph.Link(perMetre, total, "b");
        var thinned = graph.Operator("Multiply", new Vector2(x - 260f, -150f));
        graph.Link(total, thinned, "a");
        graph.Link(_inputs.Density, thinned, "b");
        graph.Link(thinned, graph.Block(spawner, "VFXSpawnerConstantRate"), "Rate");
        return spawner;
    }

    // Effect detail thins a child only through its parent's count, so a thinned source keeps its full puffs.
    private object Trigger(object parentUpdate)
    {
        var trigger = graph.Block(parentUpdate, "TriggerEvent");
        graph.Set(trigger, "mode", "OverTime");
        graph.Slot(trigger, "Rate", _layer.rate);
        var gpuEvent = graph.Context("VFXBasicGPUEvent", new Vector2(x, -200f));
        graph.Link(trigger, gpuEvent, "evt");
        return gpuEvent;
    }

    // A child rises from its parent upright, scaled by the parent's size and faded by its alpha.
    private void PlaceAtParent(object initialize)
    {
        var place = graph.Hlsl(initialize, "Place At Parent", EffectLayerParticles.Blocks + "PlaceAtBurst.hlsl");
        graph.Link(graph.SourceAttribute("position", new Vector2(x - 300f, 300f)), place, "origin");
        graph.Slot(place, "normal", Vector3.up);
        graph.Slot(place, "forward", Vector3.forward);
        graph.Link(graph.SourceAttribute("size", new Vector2(x - 300f, 420f)), place, "scale");
        var fade = graph.Block(initialize, "SetAttribute");
        graph.Set(fade, "attribute", "alpha");
        graph.Set(fade, "Composition", "Multiply");
        graph.Link(graph.SourceAttribute("alpha", new Vector2(x - 300f, 540f)), fade, "Alpha");
    }

    private object Spawner()
    {
        var spawner = graph.Context("VFXBasicSpawner", new Vector2(x, -200f));
        graph.Label(spawner, _layer.name);
        return spawner;
    }
}
