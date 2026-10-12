using System.Collections.Generic;
using System.Linq;
using HyperRTS.Editor.Maps.Nav;
using Unity.Mathematics;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Hints
{
    /// <summary>
    /// Attack lanes between bases: the distinct ground paths <see cref="MapPathCounter"/> finds from one base to
    /// another, as waypoints along each. Every base gets lanes to its nearest enemy bases only, so a map of many slots
    /// stays small.
    /// </summary>
    public static class MapHintLanes
    {
        /// <summary>Enemy bases each base gets lanes to, nearest by walking first.</summary>
        public const int MaxEnemies = 3;

        /// <summary>Distinct paths kept per pair of bases.</summary>
        public const int MaxLanesPerPair = 4;

        /// <summary>A path longer than this many times the shortest one is a detour, not a lane.</summary>
        public const float MaxDetour = 1.5f;

        /// <summary>Metres of path between waypoints.</summary>
        public const float Spacing = 50f;

        public static List<MapHintLane> Find(MapHintInput input, IReadOnlyList<MapHintBase> bases, MapIssues issues)
        {
            var lanes = new List<MapHintLane>();
            foreach (var (first, second) in Pairs(bases))
            {
                var a = bases[first];
                var b = bases[second];
                var paths = MapPathCounter.Count(input.Nav, a.Cell, b.Cell, input.CorridorWidth, input.BaseRadius,
                    MaxLanesPerPair);
                if (paths.Count == 0)
                {
                    issues.Error($"AI hints: no ground path from slot {a.Slot} to slot {b.Slot}.");
                    continue;
                }

                var limit = paths[0].Length * MaxDetour;
                foreach (var path in paths.Where(p => p.Length <= limit))
                {
                    lanes.Add(new MapHintLane(a, b, path, Waypoints(input.Nav, path)));
                }
            }

            return lanes;
        }

        /// <summary>Index pairs (lower first) of bases that get lanes: each base with its nearest enemies, in order.</summary>
        private static SortedSet<(int, int)> Pairs(IReadOnlyList<MapHintBase> bases)
        {
            var pairs = new SortedSet<(int, int)>();
            for (var i = 0; i < bases.Count; i++)
            {
                var origin = bases[i];
                var enemies = Enumerable.Range(0, bases.Count)
                    .Where(j => bases[j].Team != origin.Team && !float.IsInfinity(origin.Walk[bases[j].CellIndex]))
                    .OrderBy(j => origin.Walk[bases[j].CellIndex])
                    .ThenBy(j => bases[j].Slot)
                    .Take(MaxEnemies);
                foreach (var j in enemies)
                {
                    pairs.Add((math.min(i, j), math.max(i, j)));
                }
            }

            return pairs;
        }

        /// <summary>A waypoint every <see cref="Spacing"/> metres of path, none within half of that of the far base.</summary>
        public static List<Vector2> Waypoints(MapNavGrid nav, MapPath path)
        {
            var points = new List<Vector2>();
            var travelled = 0f;
            var next = Spacing;
            for (var i = 1; i < path.Cells.Count; i++)
            {
                travelled += math.distance(path.Cells[i - 1], path.Cells[i]) * nav.Grid.CellSize;
                if (travelled < next)
                {
                    continue;
                }

                if (path.Length - travelled < Spacing * 0.5f)
                {
                    break;
                }

                var center = nav.Center(path.Cells[i]);
                points.Add(new Vector2(center.x, center.z));
                next += Spacing;
            }

            return points;
        }
    }
}
