using System.Linq;
using HyperRTS.Editor.Maps.Land;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Planning
{
    /// <summary>
    /// A plot's fences: the front fence along the street with one gap holding the gate (in line with the house),
    /// and side fences along the yard and garden. A side that abuts a plot already fenced is left to that plot, so a
    /// shared border gets one fence. Fences are lines, not clearance-tested: they stand on the plot's own edge.
    /// </summary>
    internal static class MapPlotFences
    {
        private const float NeighbourProbe = 1f;

        public static void Build(MapPlanContext context, MapSettlementEntries entries, MapPlot plot, float gateU)
        {
            Front(context, entries, plot, gateU);
            Side(context, entries, plot, -1f);
            Side(context, entries, plot, 1f);
        }

        private static void Front(MapPlanContext context, MapSettlementEntries entries, MapPlot plot, float gateU)
        {
            var fence = entries.FrontFence;
            if (fence == null)
            {
                return;
            }

            var half = plot.Width * 0.5f;
            var centers = MapPieceRun.Centers(fence, plot.Point(-half, 0f), plot.Point(half, 0f));
            var gate = entries.Gate;
            if (gate != null && centers.Count > 0)
            {
                var gap = centers.OrderBy(c => Mathf.Abs(Along(plot, c) - gateU)).First();
                var clear = (MapPieceRun.Length(gate) + MapPieceRun.Length(fence)) * 0.5f - 0.05f;
                centers.RemoveAll(c => (c - gap).magnitude < clear);
                context.Placer.AddEverywhere(gate, gap, MapPieceRun.YawAlong(gate, plot.Run), 1f);
            }

            MapPieceRun.Place(context, fence, centers, plot.Run);
        }

        private static void Side(MapPlanContext context, MapSettlementEntries entries, MapPlot plot, float side)
        {
            var fence = entries.SideFence;
            if (fence == null || HasNeighbour(context, plot, side))
            {
                return;
            }

            var edge = plot.Width * 0.5f * side;
            var centers = MapPieceRun.Centers(fence, plot.Point(edge, 0f), plot.Point(edge, plot.GardenEnd));
            MapPieceRun.Place(context, fence, centers, plot.Back);
        }

        /// <summary>Whether another plot's yard, garden or back lies just past this side.</summary>
        private static bool HasNeighbour(MapPlanContext context, MapPlot plot, float side)
        {
            var probe = plot.Point(side * (plot.Width * 0.5f + NeighbourProbe), plot.YardEnd * 0.5f);
            var kind = context.Land.KindAt(probe);
            return kind == MapLandKind.Yard || kind == MapLandKind.Garden || kind == MapLandKind.Back;
        }

        private static float Along(MapPlot plot, Vector2 point)
        {
            plot.Local(point, out var u, out _);
            return u;
        }
    }
}
