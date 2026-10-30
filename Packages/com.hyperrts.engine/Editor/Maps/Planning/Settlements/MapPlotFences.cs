using System.Linq;
using HyperRTS.Editor.Maps.Land;
using HyperRTS.Editor.Maps.Placement;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Planning.Settlements
{
    /// <summary>
    /// A plot's fences: the front fence along the street with one gap holding the gate (in line with the house),
    /// and side fences along the yard and garden. A side that abuts a plot already fenced is left to that plot, so a
    /// shared border gets one fence. Fences are lines, not clearance-tested: they stand on the plot's own edge.
    /// </summary>
    internal sealed class MapPlotFences
    {
        private const float NeighbourProbe = 1f;
        private readonly MapSettlementEntries _entries;
        private readonly MapLand _land;
        private readonly MapPlacements _placements;

        public MapPlotFences(MapSettlement settlement)
        {
            _entries = settlement.Entries;
            _land = settlement.Context.Land;
            _placements = settlement.Context.Placements;
        }

        /// <summary>Fences the plot, its gate across from <paramref name="gateU"/> along the front; returns the gate, if any.</summary>
        public Vector2? Build(MapPlot plot, float gateU)
        {
            var gate = Front(plot, gateU);
            Side(plot, -1f);
            Side(plot, 1f);
            return gate;
        }

        private Vector2? Front(MapPlot plot, float gateU)
        {
            var fence = _entries.FrontFence;
            if (fence == null)
            {
                return null;
            }

            var half = plot.Width * 0.5f;
            var centers = MapSections.Centers(fence, plot.Point(-half, 0f), plot.Point(half, 0f));
            var gate = _entries.Gate;
            Vector2? gatePosition = null;
            if (gate != null && centers.Count > 0)
            {
                var gap = centers.OrderBy(c => Mathf.Abs(Along(plot, c) - gateU)).First();
                var clear = (MapSections.Length(gate) + MapSections.Length(fence)) * 0.5f - 0.05f;
                centers.RemoveAll(c => (c - gap).magnitude < clear);
                _placements.AddEverywhere(gate, gap, MapSections.YawAlong(gate, plot.Run), 1f, 1f);
                gatePosition = gap;
            }

            MapSections.Place(_placements, fence, centers, plot.Run);
            return gatePosition;
        }

        private void Side(MapPlot plot, float side)
        {
            var fence = _entries.SideFence;
            if (fence == null || HasNeighbour(plot, side))
            {
                return;
            }

            var edge = plot.Width * 0.5f * side;
            var centers = MapSections.Centers(fence, plot.Point(edge, 0f), plot.Point(edge, plot.GardenEnd));
            MapSections.Place(_placements, fence, centers, plot.Back);
        }

        /// <summary>Whether another plot's yard, garden or back lies just past this side.</summary>
        private bool HasNeighbour(MapPlot plot, float side)
        {
            var probe = plot.Point(side * (plot.Width * 0.5f + NeighbourProbe), plot.YardEnd * 0.5f);
            var kind = _land.KindAt(probe);
            return kind == MapLandKind.Yard || kind == MapLandKind.Garden || kind == MapLandKind.Back;
        }

        private static float Along(MapPlot plot, Vector2 point)
        {
            plot.Local(point, out var u, out _);
            return u;
        }
    }
}
