using System.Collections.Generic;
using HyperRTS.Editor.Maps.Data;
using HyperRTS.Editor.Maps.Geometry;
using HyperRTS.Editor.Maps.Land;
using HyperRTS.Editor.Maps.Placement;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Planning.Settlements
{
    /// <summary>
    /// A plot's worn paths, from the gate to the house front and round the house to each outbuilding and yard extra,
    /// worn into its yard and garden in every copy.
    /// </summary>
    internal sealed class MapPlotPaths
    {
        // Metres the path keeps off the house walls as it goes round them.
        private const float WallGap = 1f;

        private readonly MapPlanContext _context;
        private readonly MapSettlementKit _kit;

        public MapPlotPaths(MapPlanContext context, MapSettlementKit kit)
        {
            _context = context;
            _kit = kit;
        }

        public void Wear(MapPlot plot, MapPlacement house, Vector2 gate, IReadOnlyList<Vector2> spots, int seed)
        {
            if (string.IsNullOrEmpty(_kit.pathSurface))
            {
                return;
            }

            var paths = Lines(plot, house, gate, spots);
            var region = _context.Region(MapLandKind.Yard, _kit.pathSurface, seed);
            var half = _kit.pathWidth * 0.5f;
            var land = _context.Land;
            foreach (var copy in _context.Layout.Symmetry.Copies)
            {
                land.ForEachIn(plot.Bounds(copy), (x, z, center) =>
                {
                    var sector = copy.ApplyInverse(center);
                    var kind = land.Region(x, z)?.Kind;
                    var worn = kind is MapLandKind.Yard or MapLandKind.Garden && plot.Contains(sector);
                    if (worn && paths.Exists(line => MapGeometry.DistanceToPolyline(line, sector) < half))
                    {
                        land.Reclaim(x, z, region);
                    }
                });
            }
        }

        /// <summary>Gate to door, then from the door round the nearer side of the house to each spot beside or behind it.</summary>
        private static List<List<Vector2>> Lines(MapPlot plot, MapPlacement house, Vector2 gate, IReadOnlyList<Vector2> spots)
        {
            plot.Local(house.Position, out var houseU, out var houseV);
            var halfWidth = house.Entry.size.x * house.Scale * 0.5f + WallGap;
            var halfDepth = house.Entry.size.y * house.Scale * 0.5f + WallGap;
            var (front, back) = (houseV - halfDepth, houseV + halfDepth);
            var door = plot.Point(houseU, front);
            var lines = new List<List<Vector2>> { new() { gate, door } };
            foreach (var spot in spots)
            {
                plot.Local(spot, out var u, out var v);
                if (v <= front)
                {
                    lines.Add(new List<Vector2> { door, spot });
                    continue;
                }

                var side = houseU + (u >= houseU ? halfWidth : -halfWidth);
                var line = new List<Vector2> { door, plot.Point(side, front) };
                if (v > back)
                {
                    line.Add(plot.Point(side, back));
                }

                line.Add(spot);
                lines.Add(line);
            }

            return lines;
        }
    }
}
