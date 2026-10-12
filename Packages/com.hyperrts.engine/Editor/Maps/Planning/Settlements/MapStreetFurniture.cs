using HyperRTS.Editor.Maps.Data;
using HyperRTS.Editor.Maps.Geometry;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Planning.Settlements
{
    /// <summary>
    /// What lines a settlement's streets: poles every pole spacing on the left of each road and pipe sections end
    /// to end on the right, a little off the road edge, wherever the verge is clear. Chosen in the authored sector
    /// and copied.
    /// </summary>
    internal sealed class MapStreetFurniture
    {
        private const float EdgeOffset = 1.5f;
        private const float SpotClearance = 0.3f;
        private readonly MapSettlement _settlement;
        private readonly MapPlanContext _context;

        public MapStreetFurniture(MapSettlement settlement)
        {
            _settlement = settlement;
            _context = settlement.Context;
        }

        public void Place()
        {
            var entries = _settlement.Entries;
            foreach (var road in _settlement.Streets)
            {
                if (entries.Pole != null)
                {
                    Poles(entries.Pole, _settlement.Kit.poleSpacing, road);
                }

                if (entries.Pipe != null)
                {
                    Pipes(entries.Pipe, road);
                }
            }
        }

        private void Poles(MapPaletteEntry pole, float spacing, MapRoad road)
        {
            for (var along = spacing * 0.5f; along < road.Line.Length; along += spacing)
            {
                var position = Beside(road, along, 1f, out var direction);
                TryAdd(pole, position, direction);
            }
        }

        private void Pipes(MapPaletteEntry pipe, MapRoad road)
        {
            var section = MapSections.Length(pipe);
            for (var along = section * 0.5f; along + section * 0.5f <= road.Line.Length; along += section)
            {
                var position = Beside(road, along, -1f, out var direction);
                if (Clear(road, position, direction * (section * 0.5f)))
                {
                    TryAdd(pipe, position, direction);
                }
            }
        }

        /// <summary>
        /// A section stops short of a crossing road or a yard: both its ends stand on the verge, and neither end nor its
        /// middle lies on another road (at a junction the crossing road's surface can be the street's verge).
        /// </summary>
        private bool Clear(MapRoad street, Vector2 middle, Vector2 half)
        {
            if (!_settlement.OnVerge(middle - half) || !_settlement.OnVerge(middle + half))
            {
                return false;
            }

            foreach (var point in new[] { middle - half, middle, middle + half })
            {
                if (OnOtherRoad(street, point))
                {
                    return false;
                }
            }

            return true;
        }

        private bool OnOtherRoad(MapRoad street, Vector2 point)
        {
            foreach (var other in _context.Shapes.Roads)
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
            return point + MapGeometry.Left(direction) * (side * (road.Width * 0.5f + EdgeOffset));
        }

        private void TryAdd(MapPaletteEntry entry, Vector2 position, Vector2 direction)
        {
            if (!_settlement.Reaches(position))
            {
                return;
            }

            var reach = entry.Radius;
            var symmetry = _context.Layout.Symmetry;
            if (!symmetry.InSector(position) || symmetry.SeamDistance(position) < reach)
            {
                return;
            }

            var clearance = _context.Placements.Clearance;
            var standing = clearance.IsDry(position, reach) && _settlement.OnVerge(position);
            if (!standing || !clearance.IsFree(position, SpotClearance))
            {
                return;
            }

            _context.Placements.AddEverywhere(entry, position, MapSections.YawAlong(entry, direction), 1f);
        }
    }
}
