using System;
using System.Collections.Generic;
using System.Linq;
using HyperRTS.Editor.Maps.Spec;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Layout
{
    /// <summary>
    /// Copies a checked spec's sector by the symmetry: features outside the sector, shared features off the axis
    /// and anything outside the map are errors.
    /// </summary>
    public sealed class MapExpander
    {
        public const int MaxPlayers = 8;
        public const string FreeForAll = "ffa";

        private readonly MapLayout _layout;
        private readonly MapIssues _issues;

        private MapExpander(MapLayout layout, MapIssues issues)
        {
            _layout = layout;
            _issues = issues;
        }

        private MapSymmetry Symmetry => _layout.Symmetry;

        public static MapLayout Expand(MapSpec spec, MapIssues issues)
        {
            var layout = new MapLayout(spec, MapSymmetry.Parse(spec.symmetry));
            var expander = new MapExpander(layout, issues);
            expander.AddSpawns();
            layout.Resources = expander.Points(spec.resources, nameof(MapSpec.resources), "resource", r => r.shared,
                r => r.pos, (r, t) => r.pos = t.Apply(r.pos));
            layout.Props = expander.Points(spec.props, nameof(MapSpec.props), "prop", p => p.shared, p => p.pos, (p, t) =>
            {
                p.pos = t.Apply(p.pos);
                p.yaw = t.ApplyYaw(p.yaw);
            });
            layout.Hills = expander.Points(spec.hills, nameof(MapSpec.hills), "hill", h => h.shared, h => h.pos,
                (h, t) => h.pos = t.Apply(h.pos));
            layout.Settlements = expander.Points(spec.settlements, nameof(MapSpec.settlements), "settlement", _ => false,
                w => w.center, (w, t) => w.center = t.Apply(w.center));
            layout.Rivers = expander.Lines(spec.rivers, nameof(MapSpec.rivers), "river", r => r.shared, r => r.points);
            layout.Ravines = expander.Lines(spec.ravines, nameof(MapSpec.ravines), "ravine", r => r.shared, r => r.points);
            layout.Roads = expander.Lines(spec.roads, nameof(MapSpec.roads), "road", r => r.shared, r => r.points);
            layout.Woods = expander.Lines(spec.woods, nameof(MapSpec.woods), "wood", w => w.shared, w => w.points);
            layout.Belts = expander.Lines(spec.belts, nameof(MapSpec.belts), "belt", b => b.shared, b => b.points);
            layout.Farmland = expander.Lines(spec.farmland, nameof(MapSpec.farmland), "farmland", f => f.shared,
                f => f.points, true);
            layout.Areas = expander.Lines(spec.areas, nameof(MapSpec.areas), "area", a => a.shared, a => a.points);
            layout.Rows = expander.Lines(spec.rows, nameof(MapSpec.rows), "row", r => r.shared, r => r.points);
            layout.Scatters = expander.Lines(spec.scatters, nameof(MapSpec.scatters), "scatter", s => s.shared,
                s => s.points);
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

        private void AddSpawns()
        {
            var spec = _layout.Spec;
            var copies = Symmetry.Copies;
            var players = spec.spawns.Count * copies.Count;
            if (!PlayersFit(spec.mode, players))
            {
                _issues.Error($"Mode '{spec.mode}' doesn't fit {players} players ({spec.spawns.Count} spawns × {copies.Count} copies).");
            }

            var freeForAll = spec.mode == FreeForAll;
            for (var i = 0; i < spec.spawns.Count; i++)
            {
                var spawn = spec.spawns[i];
                if (!Symmetry.InSector(spawn.pos.ToVector2()))
                {
                    _issues.Error($"Spawn {i + 1} at {spawn.pos} lies outside the authored sector.",
                        new MapIssueFeature(nameof(MapSpec.spawns), i));
                }

                for (var copy = 0; copy < copies.Count; copy++)
                {
                    var transform = copies[copy];
                    var slot = Symmetry.Slot(i, copy);
                    _layout.Spawns.Add(new MapSpawn(transform.Apply(spawn.pos.ToVector2()), transform.ApplyYaw(spawn.yaw),
                        slot, MapSymmetry.Team(freeForAll, slot, copy), copy));
                }
            }

            _layout.Spawns.Sort((a, b) => a.Slot.CompareTo(b.Slot));
        }

        /// <summary>Point features: they must lie in the playable area.</summary>
        private List<MapCopy<T>> Points<T>(List<T> authored, string array, string label, Func<T, bool> shared,
            Func<T, MapPoint> anchor, Action<T, MapTransform> transform) where T : class =>
            Copy(authored, array, label, _layout.Bounds, shared, f => new[] { anchor(f).ToVector2() }, transform);

        /// <summary>
        /// Lines and areas: they may reach into the backdrop. <paramref name="mayBeEmpty"/>: no points is allowed
        /// (farmland that fills the free land).
        /// </summary>
        private List<MapCopy<T>> Lines<T>(List<T> authored, string array, string label, Func<T, bool> shared,
            Func<T, List<MapPoint>> points, bool mayBeEmpty = false) where T : class =>
            Copy(authored, array, label, _layout.TerrainBounds, shared, f => points(f).Select(p => p.ToVector2()).ToList(), (f, t) =>
            {
                var list = points(f);
                for (var i = 0; i < list.Count; i++)
                {
                    list[i] = t.Apply(list[i]);
                }
            }, mayBeEmpty);

        private List<MapCopy<T>> Copy<T>(List<T> authored, string array, string label, Rect bounds, Func<T, bool> shared,
            Func<T, IReadOnlyList<Vector2>> anchors, Action<T, MapTransform> transform, bool mayBeEmpty = false)
            where T : class
        {
            var copies = new List<MapCopy<T>>();
            for (var index = 0; index < authored.Count; index++)
            {
                var feature = authored[index];
                var points = anchors(feature);
                var unbounded = mayBeEmpty && points.Count == 0;
                var name = $"{label} {index + 1}";
                if (!unbounded && !Check(name, new MapIssueFeature(array, index), points, bounds, shared(feature)))
                {
                    continue;
                }

                var count = shared(feature) ? 1 : Symmetry.Copies.Count;
                for (var copy = 0; copy < count; copy++)
                {
                    var placed = JsonUtility.FromJson<T>(JsonUtility.ToJson(feature));
                    transform(placed, Symmetry.Copies[copy]);
                    copies.Add(new MapCopy<T>(feature, placed, Symmetry.Copies[copy], index, copy));
                }
            }

            return copies;
        }

        /// <summary>Reports where a feature lies wrong; false when it has no points to copy.</summary>
        private bool Check(string name, MapIssueFeature feature, IReadOnlyList<Vector2> points, Rect bounds, bool shared)
        {
            if (points.Count == 0)
            {
                _issues.Error($"{name} has no points.", feature);
                return false;
            }

            if (points.Any(p => Outside(bounds, p)))
            {
                var where = bounds == _layout.Bounds ? "the playable area" : "the terrain";
                _issues.Error($"{name} has points outside {where}.", feature);
            }

            if (shared && !Symmetry.IsFixed(points))
            {
                _issues.Error($"{name} is shared but not symmetric: it must sit on the axis or at the centre.", feature);
            }

            if (!shared && !points.All(Symmetry.InSector))
            {
                _issues.Error($"{name} reaches outside the authored sector; mark it shared or keep it in the sector.", feature);
            }

            return true;
        }

        /// <summary>Outside a rectangle, its edges counting as inside.</summary>
        private static bool Outside(Rect bounds, Vector2 point)
        {
            var acrossX = point.x < bounds.xMin || point.x > bounds.xMax;
            return acrossX || point.y < bounds.yMin || point.y > bounds.yMax;
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
                    crossing.along = 1f - crossing.along;
                }
            }
        }
    }
}
