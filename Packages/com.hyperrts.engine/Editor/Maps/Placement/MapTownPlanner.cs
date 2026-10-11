using System.Collections.Generic;
using System.Linq;
using HyperRTS.Editor.Maps.Data;
using HyperRTS.Editor.Maps.Geometry;
using HyperRTS.Editor.Maps.Layout;
using HyperRTS.Editor.Maps.Spec;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Placement
{
    /// <summary>
    /// Lines a town's buildings along the roads inside its radius, facing the street, nearest the centre first. Plots
    /// stay in the authored sector so the copies never overlap.
    /// </summary>
    internal sealed class MapTownPlanner
    {
        private const float MaxSlope = 16f;
        private const float YawJitter = 4f;
        private const float YardDepth = 5f;
        private readonly MapPlacer _placer;
        private readonly MapLayout _layout;
        private readonly MapShapes _shapes;
        private readonly MapPalette _palette;
        private readonly MapIssues _issues;

        public MapTownPlanner(MapPlacer placer, MapLayout layout, MapShapes shapes, MapPalette palette, MapIssues issues)
        {
            _placer = placer;
            _layout = layout;
            _shapes = shapes;
            _palette = palette;
            _issues = issues;
        }

        public void Plan()
        {
            foreach (var town in _layout.Towns.Where(t => t.Copy == 0))
            {
                var kit = _palette.TownKit(town.Authored.kind);
                if (kit == null)
                {
                    _issues.Error($"Town {town.Index + 1}: kit '{town.Authored.kind}' is not in the palette.");
                    continue;
                }

                var entries = kit.buildings.Select(key => _placer.Entry(key, $"town kit '{kit.key}'"))
                    .Where(e => e != null).ToList();
                if (entries.Count > 0)
                {
                    Build(town, kit, entries);
                }
            }
        }

        private void Build(MapCopy<MapTownSpec> town, MapTownKit kit, List<MapPaletteEntry> entries)
        {
            var random = MapPlacer.Random(_layout.Spec.seed, 5, town.Index);
            var spec = town.Authored;
            var placed = 0;
            var (blocked, crowded, steep) = (0, 0, 0);
            foreach (var (plot, facing) in Plots(spec, kit, random))
            {
                if (placed >= spec.count)
                {
                    break;
                }

                var entry = entries[random.Next(entries.Count)];
                var radius = entry.Radius;
                if (!_placer.Clearance.IsOpenGround(plot, radius))
                {
                    blocked++;
                    continue;
                }

                if (!_placer.Clearance.IsFree(plot, radius + 1f))
                {
                    crowded++;
                    continue;
                }

                if (_placer.Heights.Slope(plot, radius * 2f) > MaxSlope)
                {
                    steep++;
                    continue;
                }

                var yaw = MapGeometry.Yaw(facing) + ((float)random.NextDouble() * 2f - 1f) * YawJitter;
                _placer.AddCopies(_layout.Towns, town.Index, entry, plot, yaw, 1f);
                AddYards(town.Index, plot - facing * (radius + YardDepth));
                placed++;
            }

            if (placed < spec.count)
            {
                _issues.Warn($"Town {town.Index + 1} fit {placed} of {spec.count} buildings; plots were off open ground " +
                    $"(road, water, ravine, base or edge) {blocked} times, crowded {crowded}, too steep {steep}.");
            }
        }

        /// <summary>Plots on both sides of every road inside the radius, with the direction towards the road.</summary>
        private IEnumerable<(Vector2 Plot, Vector2 Facing)> Plots(MapTownSpec town, MapTownKit kit, System.Random random)
        {
            var center = town.center.ToVector2();
            var plots = new List<(Vector2 Plot, Vector2 Facing, float Order)>();
            foreach (var road in _shapes.Roads)
            {
                for (var along = kit.spacing * 0.5f; along < road.Length; along += kit.spacing)
                {
                    var point = MapGeometry.PointAlong(road.Points, along, out var direction);
                    if ((point - center).magnitude > town.radius)
                    {
                        continue;
                    }

                    var normal = new Vector2(direction.y, -direction.x);
                    foreach (var side in new[] { -1f, 1f })
                    {
                        var plot = point + normal * (side * kit.setback);
                        if (_layout.Symmetry.InSector(plot))
                        {
                            var order = (plot - center).magnitude + (float)random.NextDouble() * kit.spacing;
                            plots.Add((plot, -normal * side, order));
                        }
                    }
                }
            }

            return plots.OrderBy(p => p.Order).Select(p => (p.Plot, p.Facing));
        }

        private void AddYards(int index, Vector2 yard)
        {
            foreach (var copy in _layout.Towns.Where(c => c.Index == index))
            {
                _placer.Yards.Add(copy.Transform.Apply(yard));
            }
        }
    }
}
