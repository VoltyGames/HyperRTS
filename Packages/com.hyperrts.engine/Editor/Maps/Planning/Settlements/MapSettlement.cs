using System.Collections.Generic;
using HyperRTS.Editor.Maps.Geometry;
using HyperRTS.Editor.Maps.Land;
using HyperRTS.Editor.Maps.Palette;
using HyperRTS.Editor.Maps.Spec;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Planning.Settlements
{
    /// <summary>
    /// One settlement while it is planned: its spec and resolved kit, the streets it lines, the land its plots claim
    /// and why candidate plots were turned down.
    /// </summary>
    internal sealed class MapSettlement
    {
        public MapSettlement(MapPlanContext context, MapSettlementSpec spec, int index)
        {
            Context = context;
            Spec = spec;
            Index = index;
            Kit = context.Palette.SettlementKit(spec.kind);
            Entries = new MapSettlementEntries(context.Palette, context.Climate, Kit);
            Center = spec.center.ToVector2();
            Streets = StreetsNear(context, Center, spec.radius);
            Claims = new MapPlotClaims(context, Kit);
        }

        public MapPlanContext Context { get; }

        public MapSettlementSpec Spec { get; }

        /// <summary>Position of the settlement in the spec; with the seed it keys every choice.</summary>
        public int Index { get; }

        public MapSettlementKit Kit { get; }

        public MapSettlementEntries Entries { get; }

        public Vector2 Center { get; }

        /// <summary>
        /// The roads the settlement lines with plots: every authored road within its radius. Tracks the passes lay
        /// lead to fields and pads, not to houses, so they are left out.
        /// </summary>
        public List<MapRoad> Streets { get; }

        public MapPlotClaims Claims { get; }

        public MapPlotRejections Rejections { get; } = new();

        public string Label => $"Settlement {Index + 1} ('{Spec.kind}' at {Spec.center})";

        /// <summary>Whether a point lies within the settlement's radius.</summary>
        public bool Reaches(Vector2 point) => (point - Center).magnitude <= Spec.radius;

        /// <summary>True on open ground or road verge, where roadside objects may stand.</summary>
        public bool OnVerge(Vector2 point)
        {
            var kind = Context.Land.KindAt(point);
            return kind == MapLandKind.Open || kind == MapLandKind.Verge;
        }

        private static List<MapRoad> StreetsNear(MapPlanContext context, Vector2 center, float radius)
        {
            var streets = new List<MapRoad>();
            foreach (var road in context.Shapes.Roads)
            {
                if (!road.Generated && MapGeometry.DistanceToPolyline(road.Line.Points, center) <= radius)
                {
                    streets.Add(road);
                }
            }

            return streets;
        }
    }
}
