using System.Collections.Generic;
using System.Linq;
using HyperRTS.Editor.Maps.Layout;
using HyperRTS.Editor.Maps.Metrics;
using HyperRTS.Simulation.AI;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Hints
{
    /// <summary>
    /// Computes a map's bot hints from its nav grid: per slot the resource nodes by walking distance from its base
    /// (the slot's own first) and the defence points in front of it, and the attack lanes between enemy slots.
    /// </summary>
    public static class MapHintBuilder
    {
        /// <summary>How far from a resource node the walkable ground it is reached from may be (metres).</summary>
        private const float NodeReach = 12f;

        /// <summary>Fills <paramref name="target"/>; false, with the reasons in <paramref name="issues"/>, when it can't.</summary>
        public static bool Fill(AIMapHintsAuthoring target, MapHintInput input, MapIssues issues)
        {
            var bases = FindBases(input, issues);
            if (bases == null)
            {
                return false;
            }

            var reached = new HashSet<int>();
            foreach (var origin in bases)
            {
                FillExpansions(input, origin, reached, issues);
            }

            ReportUnreached(input, reached, issues);
            var lanes = MapHintLanes.Find(input, bases, issues);
            CheckLanes(bases, lanes, issues);
            foreach (var origin in bases)
            {
                origin.Defence.AddRange(MapHintDefence.Find(input, origin, lanes));
            }

            Write(target, bases, lanes);
            return true;
        }

        /// <summary>The bases in slot order with their walking distances, or null if one has no walkable ground.</summary>
        private static List<MapHintBase> FindBases(MapHintInput input, MapIssues issues)
        {
            var bases = new List<MapHintBase>();
            foreach (var spawn in input.Spawns.OrderBy(s => s.Slot))
            {
                if (!input.Nav.TryFindWalkable(spawn.Position, input.BaseRadius, out var cell))
                {
                    issues.Error($"AI hints: slot {spawn.Slot}'s base at {spawn.Position} has no walkable ground.");
                    return null;
                }

                bases.Add(new MapHintBase(spawn, cell, input.Nav.Grid.Index(cell), MapWalkField.From(input.Nav, cell)));
            }

            return bases;
        }

        /// <summary>The base's reachable nodes, its own first, then nearest to walk to first.</summary>
        private static void FillExpansions(MapHintInput input, MapHintBase origin, HashSet<int> reached,
            MapIssues issues)
        {
            var ranked = new List<(int Rank, float Walk, Vector2 Position)>();
            for (var i = 0; i < input.Nodes.Count; i++)
            {
                var node = input.Nodes[i];
                if (!input.Nav.TryFindWalkable(node.Position, NodeReach, out var cell))
                {
                    continue;
                }

                var walk = origin.Walk[input.Nav.Grid.Index(cell)];
                if (float.IsInfinity(walk))
                {
                    continue;
                }

                reached.Add(i);
                ranked.Add((node.Owner == origin.Slot ? 0 : 1, walk, node.Position));
            }

            origin.Expansions.AddRange(ranked.OrderBy(r => r.Rank).ThenBy(r => r.Walk).ThenBy(r => r.Position.x)
                .ThenBy(r => r.Position.y).Select(r => r.Position));
            if (origin.Expansions.Count == 0)
            {
                issues.Error($"AI hints: slot {origin.Slot} can walk to no resource node.");
            }
        }

        private static void ReportUnreached(MapHintInput input, HashSet<int> reached, MapIssues issues)
        {
            for (var i = 0; i < input.Nodes.Count; i++)
            {
                if (!reached.Contains(i))
                {
                    issues.Error($"AI hints: the resource node at {input.Nodes[i].Position} can't be walked to from any base.");
                }
            }
        }

        private static void CheckLanes(List<MapHintBase> bases, List<MapHintLane> lanes, MapIssues issues)
        {
            foreach (var origin in bases)
            {
                var hasEnemy = bases.Exists(b => b.Team != origin.Team);
                var hasLane = lanes.Exists(l => l.From == origin || l.To == origin);
                if (hasEnemy && !hasLane)
                {
                    issues.Error($"AI hints: slot {origin.Slot} has no attack lane to any enemy slot.");
                }
            }
        }

        /// <summary>Each lane in both directions, the reverse one's waypoints reversed, so index k is the same path both ways.</summary>
        private static void Write(AIMapHintsAuthoring target, List<MapHintBase> bases, List<MapHintLane> lanes)
        {
            target.slots.Clear();
            target.lanes.Clear();
            foreach (var origin in bases)
            {
                target.slots.Add(new AIMapHintsAuthoring.SlotHints
                {
                    slot = origin.Slot, expansions = origin.Expansions, defence = origin.Defence,
                });
            }

            foreach (var lane in lanes)
            {
                target.lanes.Add(new AIMapHintsAuthoring.LaneHints
                {
                    from = lane.From.Slot, to = lane.To.Slot, waypoints = new List<Vector2>(lane.Waypoints),
                });
                target.lanes.Add(new AIMapHintsAuthoring.LaneHints
                {
                    from = lane.To.Slot, to = lane.From.Slot, waypoints = Enumerable.Reverse(lane.Waypoints).ToList(),
                });
            }
        }
    }
}
