using System.Collections.Generic;
using System.Linq;
using HyperRTS.Editor.Maps.Data;
using HyperRTS.Editor.Maps.Geometry;
using HyperRTS.Editor.Maps.Placement;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Planning.Settlements
{
    /// <summary>
    /// Marches along both sides of each street at the kit's frontage (±jitter), laying a plot rectangle at each step
    /// and keeping the ones the settlement can hold, nearest the centre first. Everything is decided in the authored
    /// sector; land claims and neighbouring plots are checked later, as plots are accepted.
    /// </summary>
    internal static class MapPlotFinder
    {
        private const int Channel = 33;
        private const float Shortest = 0.5f;

        // A plot's copy lies across the seam at the mirrored spot, so a small gap keeps the two apart.
        private const float SeamGap = 2f;
        private static readonly float[] Depths = { 1f, 0.8f, 0.65f, Shortest };

        public static List<MapPlot> Find(MapPlanContext context, MapSettlementKit kit, IReadOnlyList<MapRoad> streets,
            Vector2 center, float radius, int settlement, MapPlotRejections rejections)
        {
            var found = new List<MapPlot>();
            for (var i = 0; i < streets.Count; i++)
            {
                foreach (var side in new[] { 1f, -1f })
                {
                    var random = MapPlacer.Random(context.Seed, Channel, settlement * 1000 + i * 2 + (side > 0f ? 0 : 1));
                    March(context, kit, streets[i], side, center, radius, random, found, rejections);
                }
            }

            return found.OrderBy(p => (p.Center - center).sqrMagnitude).ToList();
        }

        private static void March(MapPlanContext context, MapSettlementKit kit, MapRoad road, float side, Vector2 center,
            float radius, System.Random random, List<MapPlot> found, MapPlotRejections rejections)
        {
            var length = road.Line.Length;
            var along = 0f;
            while (along < length)
            {
                var frontage = Mathf.Max(1f, kit.plot.x * Jitter(kit, random));
                var depth = Mathf.Max(1f, kit.plot.y * Jitter(kit, random));
                if (along + frontage > length)
                {
                    return;
                }

                var middle = road.Line.Path.PointAt(along + frontage * 0.5f, out var direction);
                along += frontage;
                var normal = MapStreets.Left(direction) * side;
                var front = middle + normal * (road.Width * 0.5f + kit.frontGap);
                if ((front - center).magnitude > radius)
                {
                    continue;
                }

                var (plot, reason) = Fit(context, front, direction, normal, frontage, depth);
                if (reason == null)
                {
                    found.Add(plot);
                }
                else
                {
                    rejections.Add(reason, plot.Center);
                }
            }
        }

        /// <summary>
        /// The plot at full depth, else cut shorter (to <see cref="Shortest"/> of it) until its corners and the land
        /// under it are clear, the way real plots stop at a stream, a ravine or a pad. The reason is the shortest try's
        /// when none fits.
        /// </summary>
        private static (MapPlot Plot, string Reason) Fit(MapPlanContext context, Vector2 front, Vector2 direction,
            Vector2 normal, float frontage, float depth)
        {
            MapPlot plot = null;
            string reason = null;
            foreach (var share in Depths)
            {
                plot = new MapPlot(front, direction, normal, frontage, depth * share);
                reason = Reject(context, plot) ?? MapPlotClaims.Blocked(context, plot);
                if (reason == null)
                {
                    break;
                }
            }

            return (plot, reason);
        }

        private static float Jitter(MapSettlementKit kit, System.Random random) =>
            1f + kit.plotJitter * ((float)random.NextDouble() * 2f - 1f);

        /// <summary>
        /// Why a plot can't be used whatever its neighbours do, or null. Only its street front must lie in the
        /// settlement's radius: gardens run back past the houses as far as the plot is deep.
        /// </summary>
        private static string Reject(MapPlanContext context, MapPlot plot)
        {
            var corners = plot.Corners();
            var symmetry = context.Layout.Symmetry;
            foreach (var corner in corners)
            {
                if (!symmetry.InSector(corner))
                {
                    return "outside the authored sector";
                }
            }

            foreach (var corner in corners)
            {
                if (symmetry.SeamDistance(corner) < SeamGap)
                {
                    return "too close to the symmetry seam";
                }
            }

            foreach (var corner in corners)
            {
                var ground = Ground(context, corner);
                if (ground != null)
                {
                    return ground;
                }
            }

            return null;
        }

        private static string Ground(MapPlanContext context, Vector2 point)
        {
            var clearance = context.Placer.Clearance;
            if (clearance.InBase(point, 0f))
            {
                return "in a base";
            }

            if (context.Shapes.RiverEdgeDistance(point) <= 2f)
            {
                return "touching water";
            }

            if (context.Shapes.InRavine(point))
            {
                return "in a ravine";
            }

            return clearance.IsDry(point, 0f) ? null : "off the playable area";
        }
    }
}
