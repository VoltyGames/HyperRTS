using System.Collections.Generic;
using System.Linq;
using HyperRTS.Editor.Maps.Geometry;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Planning.Settlements
{
    /// <summary>
    /// Marches along both sides of each street at the kit's frontage (±jitter), laying a plot rectangle at each step
    /// and keeping the ones the settlement can hold, nearest the centre first. Everything is decided in the authored
    /// sector; land claims and neighbouring plots are checked later, as plots are accepted.
    /// </summary>
    internal sealed class MapPlotFinder
    {
        private const float Shortest = 0.5f;

        // A plot's copy lies across the seam at the mirrored spot, so a small gap keeps the two apart.
        private const float SeamGap = 2f;
        private static readonly float[] Depths = { 1f, 0.8f, 0.65f, Shortest };

        private readonly MapSettlement _settlement;
        private readonly MapPlanContext _context;
        private readonly List<MapPlot> _found = new();

        public MapPlotFinder(MapSettlement settlement)
        {
            _settlement = settlement;
            _context = settlement.Context;
        }

        public List<MapPlot> Find()
        {
            var streets = _settlement.Streets;
            for (var i = 0; i < streets.Count; i++)
            {
                foreach (var side in new[] { 1f, -1f })
                {
                    var index = _settlement.Index * 1000 + i * 2 + (side > 0f ? 0 : 1);
                    March(streets[i], side, _context.Random(MapRandomChannel.Plots, index));
                }
            }

            return _found.OrderBy(p => (p.Center - _settlement.Center).sqrMagnitude).ToList();
        }

        private void March(MapRoad road, float side, System.Random random)
        {
            var kit = _settlement.Kit;
            var length = road.Line.Length;
            var along = 0f;
            while (along < length)
            {
                var frontage = Mathf.Max(1f, kit.plot.x * Jitter(random));
                var depth = Mathf.Max(1f, kit.plot.y * Jitter(random));
                if (along + frontage > length)
                {
                    return;
                }

                var middle = road.Line.Path.PointAt(along + frontage * 0.5f, out var direction);
                along += frontage;
                var normal = MapGeometry.Left(direction) * side;
                var front = middle + normal * (road.Width * 0.5f + kit.frontGap);
                if (!_settlement.Reaches(front))
                {
                    continue;
                }

                var (plot, reason) = Fit(front, direction, normal, frontage, depth);
                if (reason == null)
                {
                    _found.Add(plot);
                }
                else
                {
                    _settlement.Rejections.Add(reason, plot.Center);
                }
            }
        }

        /// <summary>
        /// The plot at full depth, else cut shorter (to <see cref="Shortest"/> of it) until its corners and the land
        /// under it are clear, the way real plots stop at a stream, a ravine or a pad. The reason is the shortest try's
        /// when none fits.
        /// </summary>
        private (MapPlot Plot, string Reason) Fit(Vector2 front, Vector2 direction, Vector2 normal, float frontage,
            float depth)
        {
            MapPlot plot = null;
            string reason = null;
            foreach (var share in Depths)
            {
                plot = new MapPlot(front, direction, normal, frontage, depth * share);
                reason = Reject(plot) ?? _settlement.Claims.Blocked(plot);
                if (reason == null)
                {
                    break;
                }
            }

            return (plot, reason);
        }

        private float Jitter(System.Random random) => 1f + _settlement.Kit.plotJitter * random.Signed();

        /// <summary>
        /// Why a plot can't be used whatever its neighbours do, or null. Only its street front must lie in the
        /// settlement's radius: gardens run back past the houses as far as the plot is deep.
        /// </summary>
        private string Reject(MapPlot plot)
        {
            var corners = plot.Corners();
            var symmetry = _context.Layout.Symmetry;
            if (!corners.All(symmetry.InSector))
            {
                return "outside the authored sector";
            }

            if (corners.Any(corner => symmetry.SeamDistance(corner) < SeamGap))
            {
                return "too close to the symmetry seam";
            }

            return corners.Select(Ground).FirstOrDefault(ground => ground != null);
        }

        private string Ground(Vector2 point)
        {
            var clearance = _context.Placements.Clearance;
            if (clearance.InBase(point, 0f))
            {
                return "in a base";
            }

            if (_context.Shapes.RiverEdgeDistance(point) <= 2f)
            {
                return "touching water";
            }

            if (_context.Shapes.InRavine(point))
            {
                return "in a ravine";
            }

            return clearance.IsDry(point, 0f) ? null : "off the playable area";
        }
    }
}
