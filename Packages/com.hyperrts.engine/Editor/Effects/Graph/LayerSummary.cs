using System.Collections.Generic;
using System.Linq;
using HyperRTS.Editor.Effects.Layer;
using UnityEngine;
using static HyperRTS.Editor.Effects.Graph.PropertyText;

namespace HyperRTS.Editor.Effects.Graph;

/// <summary>
/// One layer's fields that matter as a few lines of <c>field=value</c>, written as <c>effect set</c> reads them, so a
/// line can be edited and pasted back.
/// </summary>
internal sealed class LayerSummary
{
    private readonly EffectLayer _layer;
    private readonly int _index;

    public LayerSummary(EffectLayer layer, int index)
    {
        _layer = layer;
        _index = index;
    }

    public List<string> Lines()
    {
        var layer = _layer;
        var lines = new List<string>
        {
            $"[{_index}] {layer.name}: kind={layer.kind} capacity={layer.capacity}{(layer.HasParent ? " parent=" + layer.parent : string.Empty)}",
            Emission(),
            $"  lifetime={Text(layer.lifetime)} size={Text(layer.size)} aspect={Text(layer.aspect)} rotation={Text(layer.rotation)} spin={Text(layer.spin)}",
            $"  color.min={Text(layer.color.min)} color.max={Text(layer.color.max)}",
            $"  origin={Text(layer.origin)} shape={layer.shape} radius={Text(layer.radius)} rim={Bool(layer.rim)} spread={Text(layer.spread)} axis={Text(layer.axis)} shapeScale={Text(layer.shapeScale)}",
            $"  speed={Text(layer.speed)} gravity={Text(layer.gravity)} drag={Text(layer.drag)} dragBySpeed={Bool(layer.dragBySpeed)} wind={Text(layer.wind)} drift={Text(layer.drift)} orbit={Text(layer.orbit)}",
            $"  noise.strength={Text(layer.noise.strength)} noise.frequency={Text(layer.noise.frequency)} noise.scroll={Text(layer.noise.scroll)}",
            $"  texture={Name(layer.texture)} motionTexture={Name(layer.motionTexture)} softness={Text(layer.softness)} orient={layer.orient} pivot={Text(layer.pivot)}",
            Flipbook(),
            $"  sizeOverLife={Text(layer.sizeOverLife)}",
            $"  alphaOverLife={Text(layer.alphaOverLife)}",
        };
        AddKindLines(lines);
        return lines;
    }

    private string Emission()
    {
        var layer = _layer;
        return layer.HasRate || layer.HasParent
            ? $"  rate={Text(layer.rate)} ratePerMetre={Text(layer.ratePerMetre)}"
            : $"  bursts={string.Join(",", layer.bursts.Select(Text))}";
    }

    private string Flipbook()
    {
        var flipbook = _layer.flipbook;
        return $"  flipbook.columns={flipbook.columns} flipbook.rows={flipbook.rows} flipbook.from={Text(flipbook.from)} flipbook.to={Text(flipbook.to)} "
            + $"flipbook.random={Bool(flipbook.random)} flipbook.randomStart={Bool(flipbook.randomStart)}";
    }

    private void AddKindLines(List<string> lines)
    {
        var layer = _layer;
        if (layer.kind is EffectLayerKind.SixWay or EffectLayerKind.Ring)
        {
            var six = layer.sixWay;
            lines.Add($"  sixWay.opacity={Text(six.opacity)} sixWay.ambient={Text(six.ambient)} sixWay.negativeTexture={Name(six.negativeTexture)} "
                + $"sixWay.emissionRamp={Name(six.emissionRamp)} sixWay.emissionColor={Text(six.emissionColor)}");
            lines.Add($"  sixWay.heatOverLife={Text(six.heatOverLife)} sixWay.minHeatOverLife={Text(six.minHeatOverLife)}");
        }

        if (layer.kind == EffectLayerKind.Spark)
        {
            lines.Add($"  stretch.length={Text(layer.stretch.length)} stretch.lengthPerSpeed={Text(layer.stretch.lengthPerSpeed)}");
        }

        if (layer.kind == EffectLayerKind.Distort)
        {
            lines.Add($"  distort.distortion={Text(layer.distort.distortion)}");
        }

        if (layer.ground)
        {
            lines.Add($"  ground=true groundHeight={Text(layer.groundHeight)} bounce={Text(layer.bounce)} friction={Text(layer.friction)}");
        }
    }

    private static string Name(Object asset) => asset == null ? "none" : asset.name;

    private static string Bool(bool value) => value ? "true" : "false";
}
