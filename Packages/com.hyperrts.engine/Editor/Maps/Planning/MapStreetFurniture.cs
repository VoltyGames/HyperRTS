using System.Collections.Generic;
using HyperRTS.Editor.Maps.Data;
using HyperRTS.Editor.Maps.Geometry;
using HyperRTS.Editor.Maps.Land;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Planning
{
    /// <summary>
    /// What lines a settlement's streets: poles every pole spacing on the left of each road and pipe sections end
    /// to end on the right, a little off the road edge, wherever the verge is clear. Chosen in the authored sector
    /// and copied.
    /// </summary>
    internal static class MapStreetFurniture
    {
        private const float EdgeOffset = 1.5f;
        private const float SpotClearance = 0.3f;

        public static void Place(MapPlanContext context, MapSettlementKit kit, MapSettlementEntries entries,
            IReadOnlyList<MapRoad> streets, Vector2 center, float radius)
        {
            foreach (var road in streets)
            {
                if (entries.Pole != null)
                {
                    Poles(context, entries.Pole, kit.poleSpacing, road, center, radius);
                }

                if (entries.Pipe != null)
                {
                    Pipes(context, entries.Pipe, road, center, radius);
                }
            }
        }

        /// <summary>True on open ground or road verge, where roadside objects may stand.</summary>
        public static bool OnVerge(MapPlanContext context, Vector2 point)
        {
            var kind = context.Land.KindAt(point);
            return kind == MapLandKind.Open || kind == MapLandKind.Verge;
        }

        private static void Poles(MapPlanContext context, MapPaletteEntry pole, float spacing, MapRoad road,
            Vector2 center, float radius)
        {
            for (var along = spacing * 0.5f; along < road.Line.Length; along += spacing)
            {
                var position = Beside(road, along, 1f, out var direction);
                TryAdd(context, pole, position, direction, center, radius);
            }
        }

        private static void Pipes(MapPlanContext context, MapPaletteEntry pipe, MapRoad road, Vector2 center, float radius)
        {
            var piece = MapPieceRun.Length(pipe);
            for (var along = piece * 0.5f; along + piece * 0.5f <= road.Line.Length; along += piece)
            {
                var position = Beside(road, along, -1f, out var direction);
                if (Clear(context, road, position, direction * (piece * 0.5f)))
                {
                    TryAdd(context, pipe, position, direction, center, radius);
                }
            }
        }

        /// <summary>
        /// A section stops short of a crossing road or a yard: both its ends stand on the verge, and neither end nor its
        /// middle lies on another road (at a junction the crossing road's surface can be the street's verge).
        /// </summary>
        private static bool Clear(MapPlanContext context, MapRoad street, Vector2 middle, Vector2 half)
        {
            if (!OnVerge(context, middle - half) || !OnVerge(context, middle + half))
            {
                return false;
            }

            foreach (var point in new[] { middle - half, middle, middle + half })
            {
                if (OnOtherRoad(context, street, point))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool OnOtherRoad(MapPlanContext context, MapRoad street, Vector2 point)
        {
            foreach (var other in context.Shapes.Roads)
            {
                if (other != street && other.Line.Distance(point) < other.Width * 0.5f + EdgeOffset)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>The spot beside the road edge, to its left (side 1) or right (side -1), and the road's direction there.</summary>
        private static Vector2 Beside(MapRoad road, float along, float side, out Vector2 direction)
        {
            var point = road.Line.Path.PointAt(along, out direction);
            return point + MapStreets.Left(direction) * (side * (road.Width * 0.5f + EdgeOffset));
        }

        private static void TryAdd(MapPlanContext context, MapPaletteEntry entry, Vector2 position, Vector2 direction,
            Vector2 center, float radius)
        {
            if ((position - center).magnitude > radius)
            {
                return;
            }

            var reach = entry.Radius;
            var symmetry = context.Layout.Symmetry;
            if (!symmetry.InSector(position) || symmetry.SeamDistance(position) < reach)
            {
                return;
            }

            var clearance = context.Placer.Clearance;
            var standable = clearance.IsDry(position, reach) && !context.Shapes.InRavine(position);
            if (!standable || !OnVerge(context, position) || !clearance.IsFree(position, SpotClearance))
            {
                return;
            }

            context.Placer.AddEverywhere(entry, position, MapPieceRun.YawAlong(entry, direction), 1f);
        }
    }
}
