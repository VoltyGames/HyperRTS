using System;
using HyperRTS.Editor.Maps.Kits;
using HyperRTS.Editor.Maps.Land;
using HyperRTS.Editor.Maps.Layout;
using HyperRTS.Editor.Maps.Palette;

namespace HyperRTS.Editor.Maps.Planning.Settlements;

/// <summary>
/// Checks that only open ground and road verge lie under a plot, and claims its yard, garden and back in every
/// copy of the sector.
/// </summary>
internal sealed class MapPlotClaims(MapPlanContext context, MapSettlementKit kit)
{
    /// <summary>Plots may overlap a neighbour's edge by this much (metres) before it counts as overlap.</summary>
    private const float Inset = 0.6f;

    /// <summary>What a plot is blocked by, most telling first; any other kind of land ranks last.</summary>
    private static readonly MapLandKind[] Priority =
    [
        MapLandKind.Water, MapLandKind.Bank, MapLandKind.Ravine, MapLandKind.RavineRim, MapLandKind.Base,
        MapLandKind.Pad, MapLandKind.Road, MapLandKind.Yard, MapLandKind.Garden, MapLandKind.Back,
    ];

    private MapLand Land => context.Land;

    /// <summary>Why the land under a plot is taken (the most telling use found), or null when it is free.</summary>
    public string Blocked(MapPlot plot)
    {
        var worst = MapLandKind.Open;
        var worstRank = int.MaxValue;
        Land.ForEachIn(plot.Bounds(MapTransform.Identity), (x, z, center) =>
        {
            if (!plot.Contains(center, Inset))
            {
                return;
            }

            var kind = Land.Region(x, z)?.Kind ?? MapLandKind.Open;
            if (kind == MapLandKind.Open || kind == MapLandKind.Verge)
            {
                return;
            }

            var rank = Array.IndexOf(Priority, kind);
            rank = rank < 0 ? Priority.Length : rank;
            if (rank < worstRank)
            {
                worst = kind;
                worstRank = rank;
            }
        });
        return worstRank == int.MaxValue ? null : Describe(worst);
    }

    /// <summary>Claims the plot's free cells for its yard, garden and back, in every copy.</summary>
    public void Claim(MapPlot plot, int seed)
    {
        var yard = context.Region(MapLandKind.Yard, kit.yardSurface, seed);
        var back = context.Region(MapLandKind.Back, kit.backSurface, seed);
        var garden = context.Region(MapLandKind.Garden, kit.gardenSurface, seed, plot.RunYaw);
        foreach (var copy in context.Layout.Symmetry.Copies)
        {
            Land.ForEachIn(plot.Bounds(copy), (x, z, center) =>
            {
                var sector = copy.ApplyInverse(center);
                if (!plot.Contains(sector))
                {
                    return;
                }

                plot.Local(sector, out _, out var v);
                Land.Claim(x, z, ZoneRegion(plot.ZoneAt(v), yard, garden, back));
            });
        }
    }

    private static int ZoneRegion(MapPlotZone zone, int yard, int garden, int back)
    {
        if (zone == MapPlotZone.Yard)
        {
            return yard;
        }

        return zone == MapPlotZone.Garden ? garden : back;
    }

    private static string Describe(MapLandKind kind) => kind switch
    {
        MapLandKind.Water or MapLandKind.Bank => "touching water",
        MapLandKind.Ravine or MapLandKind.RavineRim => "in a ravine",
        MapLandKind.Base => "in a base",
        MapLandKind.Pad => "on a pad",
        MapLandKind.Road => "on a road",
        MapLandKind.Yard or MapLandKind.Garden or MapLandKind.Back => "overlapping another plot",
        _ => $"on {kind} land",
    };
}
