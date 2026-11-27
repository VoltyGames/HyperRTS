using HyperRTS.Editor.Maps.Geometry;
using HyperRTS.Editor.Maps.Land;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Planning;

/// <summary>
/// River banks and the floodplain meadows along them inside the valley, claimed once roads have taken their
/// crossings.
/// </summary>
public sealed class MapBankPlanner(MapPlanContext context) : MapLandPass(context)
{
    public override void Plan()
    {
        var rivers = Context.Shapes.Rivers;
        if (rivers.Count == 0)
        {
            return;
        }

        var bank = Context.Region(MapLandKind.Bank, Climate.shoreSurface);
        var floodplain = Context.Region(MapLandKind.Floodplain, Climate.floodplainSurface);
        var noise = Context.Noise(MapNoiseChannel.Banks);
        foreach (var river in rivers)
        {
            var valley = river.Spec.valleyWidth * 0.5f;
            Land.ClaimEach(river.Line.Bounds, p =>
            {
                var edgeDistance = river.EdgeDistance(p);
                var edge = edgeDistance + noise.Sample(p, 9f) * 1.5f;
                var width = ShoreWidth(noise, p);
                if (edge < width)
                {
                    return bank;
                }

                var reach = width + Climate.floodplainWidth * (1f + 0.4f * noise.Sample(p, 30f));
                var onFloodplain = edgeDistance < reach && river.Line.Distance(p) < valley;
                return onFloodplain ? floodplain : MapLand.Unclaimed;
            });
        }
    }

    // Banks widen into muddy beaches and narrow to where the meadow meets the water, over a few river widths.
    private float ShoreWidth(MapNoise noise, Vector2 p)
    {
        // Map noise sits about 0.2 either side of -0.1 (MapNoise.Blotches), so this spreads it over -1 to 1.
        var swing = Mathf.Clamp((noise.Sample(p, 70f) + 0.1f) / 0.2f, -1f, 1f);
        return Climate.shoreWidth * (1f + Climate.shoreWidthVariation * swing);
    }
}
