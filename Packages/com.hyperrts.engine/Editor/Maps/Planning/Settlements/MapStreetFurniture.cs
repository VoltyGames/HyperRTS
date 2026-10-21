using System.Collections.Generic;
using HyperRTS.Editor.Maps.Data;
using HyperRTS.Editor.Maps.Geometry;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Planning.Settlements
{
    /// <summary>
    /// What lines a settlement's streets: poles every pole spacing on the left of each road and pipe sections end
    /// to end on the right, a little off the road edge, wherever the verge is clear, in runs of at least the kit's
    /// minimum section count with arches over driveways and crossing roads. Chosen in the authored sector and copied.
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

        private void Poles(MapProp pole, float spacing, MapRoad road)
        {
            for (var along = spacing * 0.5f; along < road.Line.Length; along += spacing)
            {
                var position = Beside(road, along, 1f, out var direction);
                TryAdd(pole, position, direction);
            }
        }

        /// <summary>
        /// Decides every section along the street first, then lays the runs long enough (see <see cref="MapPipeRuns"/>)
        /// and an arch over each short gap inside a run.
        /// </summary>
        private void Pipes(MapProp pipe, MapRoad road)
        {
            var arch = _settlement.Entries.PipeArch;
            var section = MapSections.Length(pipe);
            var sections = new List<(float Along, Vector2 Position, Vector2 Direction)>();
            var pass = new List<bool>();
            for (var along = section * 0.5f; along + section * 0.5f <= road.Line.Length; along += section)
            {
                var position = Beside(road, along, -1f, out var direction);
                sections.Add((along, position, direction));
                pass.Add(Clear(road, position, direction * (section * 0.5f)) && CanStand(pipe, position));
            }

            var arches = new List<(int First, int Count)>();
            var maxGapSections = Mathf.FloorToInt(_settlement.Kit.streetPipeMaxGap / section);
            var kept = MapPipeRuns.SectionsToLay(pass, _settlement.Kit.streetPipeMinSections, maxGapSections, arches);
            for (var i = 0; i < kept.Length; i++)
            {
                if (kept[i])
                {
                    Add(pipe, sections[i].Position, sections[i].Direction, 1f);
                }
            }

            foreach (var (first, count) in arches)
            {
                var middle = Beside(road, sections[first].Along + (count - 1) * section * 0.5f, -1f, out var direction);
                if (InSettlementSector(arch, middle))
                {
                    Add(arch, middle, direction, MapPipeRuns.ArchStretch(count, section, MapSections.Length(arch)));
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

        private void TryAdd(MapProp entry, Vector2 position, Vector2 direction)
        {
            if (CanStand(entry, position))
            {
                Add(entry, position, direction, 1f);
            }
        }

        /// <summary>The spot is in the settlement and the authored sector, clear of the seam, dry, on the verge and free.</summary>
        private bool CanStand(MapProp entry, Vector2 position)
        {
            if (!InSettlementSector(entry, position))
            {
                return false;
            }

            var clearance = _context.Placements.Clearance;
            var standing = clearance.IsDry(position, entry.Radius) && _settlement.OnVerge(position);
            return standing && clearance.IsFree(position, SpotClearance);
        }

        /// <summary>The spot is in the settlement and the authored sector, at least the entry's reach off the seam.</summary>
        private bool InSettlementSector(MapProp entry, Vector2 position)
        {
            if (!_settlement.Reaches(position))
            {
                return false;
            }

            var symmetry = _context.Layout.Symmetry;
            return symmetry.InSector(position) && symmetry.SeamDistance(position) >= entry.Radius;
        }

        private void Add(MapProp entry, Vector2 position, Vector2 direction, float stretch) =>
            _context.Placements.AddEverywhere(entry, position, MapSections.YawAlong(entry, direction), 1f, stretch);
    }
}
