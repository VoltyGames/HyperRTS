using System;
using HyperRTS.Editor.Maps.Data;
using HyperRTS.Editor.Maps.Land;
using HyperRTS.Editor.Maps.Symmetry;

namespace HyperRTS.Editor.Maps.Planning
{
    /// <summary>
    /// A plot and the land: whether anything but open ground and road verge lies under it, and claiming its yard,
    /// garden and back in every copy of the sector. Cells are decided from the authored-sector point, so copies match.
    /// </summary>
    internal static class MapPlotClaims
    {
        /// <summary>Plots may overlap a neighbour's edge by this much (metres) before it counts as overlap.</summary>
        private const float Inset = 0.6f;

        /// <summary>What a plot is blocked by, most telling first; any other kind of land ranks last.</summary>
        private static readonly MapLandKind[] Priority =
        {
            MapLandKind.Water, MapLandKind.Bank, MapLandKind.Ravine, MapLandKind.RavineRim, MapLandKind.Base,
            MapLandKind.Pad, MapLandKind.Road, MapLandKind.Yard, MapLandKind.Garden, MapLandKind.Back,
        };

        /// <summary>Why the land under a plot is taken (the most telling use found), or null when it is free.</summary>
        public static string Blocked(MapPlanContext context, MapPlot plot)
        {
            var land = context.Land;
            var worst = MapLandKind.Open;
            var worstRank = int.MaxValue;
            land.ForEachIn(plot.Bounds(MapTransform.Identity), (x, z, center) =>
            {
                if (!plot.Contains(center, Inset))
                {
                    return;
                }

                var kind = land.Region(x, z)?.Kind ?? MapLandKind.Open;
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
        public static void Claim(MapPlanContext context, MapPlot plot, MapSettlementKit kit, int seed)
        {
            var source = $"settlement kit '{kit.key}'";
            var land = context.Land;
            var yard = context.Region(MapLandKind.Yard, kit.yardSurface, source, seed);
            var back = context.Region(MapLandKind.Back, kit.backSurface, source, seed);
            foreach (var copy in context.Layout.Symmetry.Copies)
            {
                var garden = context.Region(MapLandKind.Garden, kit.gardenSurface, source, seed, copy.ApplyYaw(plot.RunYaw));
                var transform = copy;
                land.ForEachIn(plot.Bounds(copy), (x, z, center) =>
                {
                    var sector = transform.ApplyInverse(center);
                    if (!plot.Contains(sector))
                    {
                        return;
                    }

                    plot.Local(sector, out _, out var v);
                    land.Claim(x, z, RegionOf(plot.ZoneAt(v), yard, garden, back));
                });
            }
        }

        private static int RegionOf(MapPlotZone zone, int yard, int garden, int back)
        {
            if (zone == MapPlotZone.Yard)
            {
                return yard;
            }

            return zone == MapPlotZone.Garden ? garden : back;
        }

        private static string Describe(MapLandKind kind)
        {
            switch (kind)
            {
                case MapLandKind.Water:
                case MapLandKind.Bank:
                    return "touching water";
                case MapLandKind.Ravine:
                case MapLandKind.RavineRim:
                    return "in a ravine";
                case MapLandKind.Base:
                    return "in a base";
                case MapLandKind.Pad:
                    return "on a pad";
                case MapLandKind.Road:
                    return "on a road";
                case MapLandKind.Yard:
                case MapLandKind.Garden:
                case MapLandKind.Back:
                    return "overlapping another plot";
                default:
                    return $"on {kind} land";
            }
        }
    }
}
