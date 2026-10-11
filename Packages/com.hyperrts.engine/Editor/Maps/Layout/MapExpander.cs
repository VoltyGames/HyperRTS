using System;
using System.Collections.Generic;
using System.Linq;
using HyperRTS.Editor.Maps.Spec;
using HyperRTS.Editor.Maps.Symmetry;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Layout
{
    /// <summary>
    /// Checks a spec and copies its sector by the symmetry: features outside the sector, shared features off the axis
    /// and anything outside the map are errors.
    /// </summary>
    public static class MapExpander
    {
        public const int MaxPlayers = 8;
        public const string FreeForAll = "ffa";

        public static MapLayout Expand(MapSpec spec, MapIssues issues)
        {
            CheckHeader(spec, issues);
            var layout = new MapLayout(spec, MapSymmetry.Parse(spec.symmetry));
            AddSpawns(layout, issues);
            var expander = new Expansion(layout, issues);
            layout.Resources = expander.Points(spec.resources, "resource", r => r.shared, r => r.pos,
                (r, t) => r.pos = t.Apply(r.pos));
            layout.Props = expander.Points(spec.props, "prop", p => p.shared, p => p.pos, (p, t) =>
            {
                p.pos = t.Apply(p.pos);
                p.yaw = t.ApplyYaw(p.yaw);
            });
            layout.Hills = expander.Points(spec.hills, "hill", h => h.shared, h => h.pos, (h, t) => h.pos = t.Apply(h.pos));
            layout.Settlements = expander.Points(spec.settlements, "settlement", _ => false, w => w.center,
                (w, t) => w.center = t.Apply(w.center));
            layout.Rivers = expander.Lines(spec.rivers, "river", r => r.shared, r => r.points);
            layout.Ravines = expander.Lines(spec.ravines, "ravine", r => r.shared, r => r.points);
            layout.Roads = expander.Lines(spec.roads, "road", r => r.shared, r => r.points);
            layout.Woods = expander.Lines(spec.woods, "wood", w => w.shared, w => w.points);
            layout.Belts = expander.Lines(spec.belts, "belt", b => b.shared, b => b.points);
            layout.Farmland = expander.Lines(spec.farmland, "farmland", f => f.shared, f => f.points, true);
            layout.Areas = expander.Lines(spec.areas, "area", a => a.shared, a => a.points);
            layout.Lines = expander.Lines(spec.lines, "line", l => l.shared, l => l.points);
            layout.Scatters = expander.Lines(spec.scatters, "scatter", s => s.shared, s => s.points);
            ContinueRivers(layout.Rivers);
            return layout;
        }

        /// <summary>Players a mode needs; FFA takes 3 to 8.</summary>
        public static bool PlayersFit(string mode, int players) => mode switch
        {
            "1v1" => players == 2,
            "2v2" => players == 4,
            "3v3" => players == 6,
            "4v4" => players == 8,
            FreeForAll => players >= 3 && players <= MaxPlayers,
            _ => false,
        };

        private static void CheckHeader(MapSpec spec, MapIssues issues)
        {
            if (string.IsNullOrEmpty(spec.name) || string.IsNullOrEmpty(spec.climate))
            {
                throw new ArgumentException("A spec needs a name and a climate.");
            }

            if (spec.size.x <= 0f || spec.size.z <= 0f)
            {
                throw new ArgumentException($"Map size {spec.size} must be positive.");
            }

            var terrain = spec.terrain;
            if (terrain.waterLevel >= terrain.height || terrain.height >= terrain.maxHeight)
            {
                issues.Error("Terrain heights must rise: waterLevel < height < maxHeight.");
            }

            if (!Mathf.IsPowerOfTwo(terrain.resolution - 1))
            {
                issues.Error($"Terrain resolution {terrain.resolution} must be 2^n + 1.");
            }
        }

        private static void AddSpawns(MapLayout layout, MapIssues issues)
        {
            var symmetry = layout.Symmetry;
            var spawns = layout.Spec.spawns;
            var players = spawns.Count * symmetry.Copies.Count;
            if (!PlayersFit(layout.Spec.mode, players))
            {
                issues.Error($"Mode '{layout.Spec.mode}' doesn't fit {players} players ({spawns.Count} spawns × {symmetry.Copies.Count} copies).");
            }

            var freeForAll = layout.Spec.mode == FreeForAll;
            for (var i = 0; i < spawns.Count; i++)
            {
                var spawn = spawns[i];
                if (!symmetry.InSector(spawn.pos.ToVector2()))
                {
                    issues.Error($"Spawn {i + 1} at {spawn.pos} lies outside the authored sector.");
                }

                for (var copy = 0; copy < symmetry.Copies.Count; copy++)
                {
                    var transform = symmetry.Copies[copy];
                    var slot = symmetry.Slot(i, copy);
                    layout.Spawns.Add(new MapSpawn(transform.Apply(spawn.pos.ToVector2()), transform.ApplyYaw(spawn.yaw),
                        slot, MapSymmetry.Team(freeForAll, slot, copy), copy));
                }
            }

            layout.Spawns.Sort((a, b) => a.Slot.CompareTo(b.Slot));
        }

        /// <summary>A river copy that ends where the authored river ends is turned around, so the water keeps flowing.</summary>
        private static void ContinueRivers(List<MapCopy<MapRiverSpec>> rivers)
        {
            foreach (var river in rivers.Where(r => r.Copy > 0))
            {
                var authored = river.Authored.points;
                var placed = river.Placed.points;
                if ((placed[^1].ToVector2() - authored[^1].ToVector2()).sqrMagnitude > 1f)
                {
                    continue;
                }

                placed.Reverse();
                foreach (var crossing in river.Placed.crossings)
                {
                    crossing.t = 1f - crossing.t;
                }
            }
        }

        private sealed class Expansion
        {
            private readonly MapLayout _layout;
            private readonly MapIssues _issues;

            public Expansion(MapLayout layout, MapIssues issues)
            {
                _layout = layout;
                _issues = issues;
            }

            public List<MapCopy<T>> Points<T>(List<T> authored, string label, Func<T, bool> shared,
                Func<T, MapPoint> anchor, Action<T, MapTransform> transform) where T : class =>
                Expand(authored, label, _layout.Bounds, shared, f => new[] { anchor(f).ToVector2() }, transform);

            /// <summary><paramref name="mayBeEmpty"/>: no points is allowed (farmland that fills the free land).</summary>
            public List<MapCopy<T>> Lines<T>(List<T> authored, string label, Func<T, bool> shared,
                Func<T, List<MapPoint>> points, bool mayBeEmpty = false) where T : class =>
                Expand(authored, label, _layout.TerrainBounds, shared, f => points(f).Select(p => p.ToVector2()).ToList(), (f, t) =>
                {
                    var list = points(f);
                    for (var i = 0; i < list.Count; i++)
                    {
                        list[i] = t.Apply(list[i]);
                    }
                }, mayBeEmpty);

            /// <summary>Gameplay points must lie in the playable area; lines and areas may reach into the margin.</summary>
            private List<MapCopy<T>> Expand<T>(List<T> authored, string label, Rect bounds, Func<T, bool> shared,
                Func<T, IReadOnlyList<Vector2>> anchors, Action<T, MapTransform> transform, bool mayBeEmpty = false)
                where T : class
            {
                var copies = new List<MapCopy<T>>();
                var symmetry = _layout.Symmetry;
                for (var index = 0; index < authored.Count; index++)
                {
                    var feature = authored[index];
                    var points = anchors(feature);
                    var unbounded = mayBeEmpty && points.Count == 0;
                    if (!unbounded && !Check(label, index, points, bounds, shared(feature)))
                    {
                        continue;
                    }

                    var count = shared(feature) ? 1 : symmetry.Copies.Count;
                    for (var copy = 0; copy < count; copy++)
                    {
                        var placed = JsonUtility.FromJson<T>(JsonUtility.ToJson(feature));
                        transform(placed, symmetry.Copies[copy]);
                        copies.Add(new MapCopy<T>(feature, placed, symmetry.Copies[copy], index, copy));
                    }
                }

                return copies;
            }

            private bool Check(string label, int index, IReadOnlyList<Vector2> points, Rect bounds, bool shared)
            {
                var name = $"{label} {index + 1}";
                if (points.Count == 0)
                {
                    _issues.Error($"{name} has no points.");
                    return false;
                }

                if (points.Any(p => p.x < bounds.xMin || p.x > bounds.xMax || p.y < bounds.yMin || p.y > bounds.yMax))
                {
                    _issues.Error($"{name} has points outside {(bounds == _layout.Bounds ? "the playable area" : "the terrain")}.");
                }

                if (shared)
                {
                    if (!_layout.Symmetry.IsFixed(points))
                    {
                        _issues.Error($"{name} is shared but not symmetric: it must sit on the axis or at the centre.");
                    }

                    return true;
                }

                if (points.Any(p => !_layout.Symmetry.InSector(p)))
                {
                    _issues.Error($"{name} reaches outside the authored sector; mark it shared or keep it in the sector.");
                }

                return true;
            }
        }
    }
}
