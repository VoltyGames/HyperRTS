using System.Collections.Generic;
using System.Linq;
using HyperRTS.Editor.Maps.Common;
using HyperRTS.Editor.Maps.Nav;
using HyperRTS.Simulation.Buildings;
using HyperRTS.Simulation.Navigation.Areas;
using HyperRTS.Simulation.Navigation.Docks;
using HyperRTS.Simulation.Navigation.Grid;
using Unity.Mathematics;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Report
{
    /// <summary>
    /// Checks that the docking hull fits every placed dock's bays, at the stop and along the run-in, clear of every
    /// wall but the dock's own footprint, so no harvester stops short of a bay.
    /// </summary>
    public sealed class MapDockCheck
    {
        private readonly MapNavGrid _nav;
        private readonly MapDockingHull _hull;
        private readonly MapIssues _issues;
        private readonly List<(DockAuthoring Dock, NavBox? Footprint)> _docks;

        private MapDockCheck(IEnumerable<DockAuthoring> docks, MapNavGrid nav, MapDockingHull hull, MapIssues issues)
        {
            _nav = nav;
            _hull = hull;
            _issues = issues;
            _docks = new List<(DockAuthoring Dock, NavBox? Footprint)>();
            foreach (var dock in docks)
            {
                if (dock.IsComplete)
                {
                    _docks.Add((dock, Footprint(dock)));
                }
            }
        }

        /// <summary>Reports each bay the hull can't reach as an error naming the dock, the bay and what it hits.</summary>
        public static void Check(IEnumerable<DockAuthoring> docks, MapNavGrid nav, MapDockingHull hull,
            MapIssues issues)
        {
            new MapDockCheck(docks, nav, hull, issues).CheckAll();
        }

        private void CheckAll()
        {
            foreach (var (dock, footprint) in _docks)
            {
                CheckBays(dock, footprint);
            }
        }

        private void CheckBays(DockAuthoring dock, NavBox? footprint)
        {
            var bays = dock.Bays();
            for (var i = 0; i < bays.Length; i++)
            {
                var stop = ((float3)dock.transform.TransformPoint(bays[i].Stop)).xz;
                var facing = math.normalizesafe(((float3)dock.transform.TransformDirection(
                    new float3(bays[i].Facing.x, 0f, bays[i].Facing.y))).xz);
                var back = facing * _hull.HalfSegment;
                if (TryFindHit(stop - back, stop + back, footprint, out var hit))
                {
                    Report(dock, i, "at its stop", hit);
                }
                else if (TryFindHit(stop - facing * bays[i].RunIn - back, stop + back, footprint, out hit))
                {
                    Report(dock, i, "on its run-in", hit);
                }
            }
        }

        // The blocked cell the hull's core from a to b comes within its radius of, skipping the dock's own cells.
        private bool TryFindHit(float2 a, float2 b, NavBox? own, out int2 hit)
        {
            var grid = _nav.Grid;
            var cellSize = grid.Grid.CellSize;
            var reach = _hull.Radius + cellSize;
            var cells = grid.Grid.Cells(grid.Grid.Cell(math.min(a, b) - reach), grid.Grid.Cell(math.max(a, b) + reach));
            foreach (var cell in cells)
            {
                if (_nav.IsWalkable(cell) || Covers(own, cell))
                {
                    continue;
                }

                var square = new NavBox(new float3(grid.Grid.Center(cell).x, 0f, grid.Grid.Center(cell).y),
                    quaternion.identity, new float2(cellSize));
                if (square.Gap(a, b) < _hull.Radius - NavTolerances.WallOverlap)
                {
                    hit = cell;
                    return true;
                }
            }

            hit = default;
            return false;
        }

        private void Report(DockAuthoring dock, int bay, string where, int2 cell)
        {
            var point = _nav.Grid.Grid.Center(cell);
            var other = _docks.Where(entry => entry.Dock != dock && Covers(entry.Footprint, cell))
                .Select(entry => $"'{entry.Dock.name}' at {Position(entry.Dock)}").FirstOrDefault();
            var hits = other ?? $"blocked ground at ({point.x:0.#}, {point.y:0.#})";
            _issues.Error($"Dock '{dock.name}' at {Position(dock)}, bay {bay + 1}: a {_hull.Length:0.#} m hull " +
                          $"({_hull.Prefabs}) {where} hits {hits}; move the dock or what it hits apart.");
        }

        private static string Position(Component dock) =>
            $"({dock.transform.position.x:0.#}, {dock.transform.position.z:0.#})";

        private bool Covers(NavBox? footprint, int2 cell)
        {
            if (footprint == null)
            {
                return false;
            }

            var under = _nav.Grid.CellsUnder(footprint.Value);
            return math.all(cell >= under.Min & cell <= under.Max) && _nav.Grid.InBox(footprint.Value, cell);
        }

        private static NavBox? Footprint(DockAuthoring dock)
        {
            var transform = dock.transform;
            if (dock.TryGetComponent<BuildingAuthoring>(out var building))
            {
                return new NavBox(transform.position, transform.rotation, building.footprint);
            }

            if (dock.TryGetComponent<NavObstacleAuthoring>(out var obstacle))
            {
                return new NavBox(transform.position, transform.rotation, obstacle.size);
            }

            return null;
        }
    }
}
