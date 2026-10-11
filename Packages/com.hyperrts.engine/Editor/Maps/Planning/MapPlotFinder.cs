using System.Collections.Generic;
using System.Linq;
using HyperRTS.Editor.Maps.Data;
using HyperRTS.Editor.Maps.Geometry;
using HyperRTS.Editor.Maps.Placement;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Planning
{
    /// <summary>
    /// Marches along both sides of each street at the kit's frontage (±jitter), laying a plot rectangle at each step
    /// and keeping the ones the settlement can hold, nearest the centre first. Everything is decided in the authored
    /// sector; land claims and neighbouring plots are checked later, as plots are accepted.
    /// </summary>
    internal static class MapPlotFinder
    {
        private const int Channel = 33;

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

                var middle = MapGeometry.PointAlong(road.Line.Points, along + frontage * 0.5f, out var direction);
                along += frontage;
                var normal = MapStreets.Left(direction) * side;
                var front = middle + normal * (road.Width * 0.5f + kit.frontGap);
                var plot = new MapPlot(front, direction, normal, frontage, depth);
                if (!Reaches(plot, center, radius))
                {
                    continue;
                }

                var reason = Reject(context, plot, center, radius);
                if (reason == null)
                {
                    found.Add(plot);
                }
                else
                {
                    rejections.Add(reason);
                }
            }
        }

        private static float Jitter(MapSettlementKit kit, System.Random random) =>
            1f + kit.plotJitter * ((float)random.NextDouble() * 2f - 1f);

        /// <summary>Whether any part of the plot lies in the settlement's circle; plots wholly outside aren't candidates.</summary>
        private static bool Reaches(MapPlot plot, Vector2 center, float radius)
        {
            foreach (var corner in plot.Corners())
            {
                if ((corner - center).magnitude <= radius)
                {
                    return true;
                }
            }

            return (plot.Center - center).magnitude <= radius;
        }

        /// <summary>Why a plot can't be used whatever its neighbours do, or null.</summary>
        private static string Reject(MapPlanContext context, MapPlot plot, Vector2 center, float radius)
        {
            var corners = plot.Corners();
            var symmetry = context.Layout.Symmetry;
            foreach (var corner in corners)
            {
                if ((corner - center).magnitude > radius)
                {
                    return "past the radius";
                }
            }

            foreach (var corner in corners)
            {
                if (!symmetry.InSector(corner))
                {
                    return "outside the authored sector";
                }
            }

            foreach (var corner in corners)
            {
                if (symmetry.SeamDistance(corner) < plot.Width * 0.5f)
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
