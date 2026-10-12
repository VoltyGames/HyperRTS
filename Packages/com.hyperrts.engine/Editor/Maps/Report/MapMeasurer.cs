using System.Collections.Generic;
using System.Linq;
using HyperRTS.Editor.Maps.Data;
using HyperRTS.Editor.Maps.Layout;
using HyperRTS.Editor.Maps.Nav;
using HyperRTS.Editor.Maps.Placement;
using HyperRTS.Simulation.Navigation;
using Unity.Mathematics;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Report
{
    /// <summary>
    /// Measures a built map against the rules: rush times to the nearest enemy, attack paths, reachability of every
    /// placed object, buildable ground and resources per base.
    /// </summary>
    public sealed class MapMeasurer
    {
        private const float FairnessTolerance = 0.03f;
        private const float FieldReach = 2.5f;
        private readonly MapLayout _layout;
        private readonly MapNavGrid _nav;
        private readonly MapPalette _palette;
        private readonly MapRules _rules;
        private readonly MapIssues _issues;
        private readonly List<int2> _spawnCells = new();

        private MapMeasurer(MapLayout layout, MapNavGrid nav, MapPalette palette, MapRules rules, MapIssues issues)
        {
            _layout = layout;
            _nav = nav;
            _palette = palette;
            _rules = rules;
            _issues = issues;
        }

        /// <summary>The attack paths found between slots 1 and 2, for the overview image.</summary>
        public List<MapPath> Paths { get; private set; } = new();

        public MapMetrics Metrics { get; } = new();

        public static MapMeasurer Measure(MapLayout layout, MapNavGrid nav, MapPlacer placer, int trees,
            MapPalette palette, MapRules rules, MapIssues issues)
        {
            var measurer = new MapMeasurer(layout, nav, palette, rules, issues);
            measurer.Metrics.trees = trees;
            measurer.Metrics.placements = placer.Placements.Count;
            if (measurer.FindSpawnCells())
            {
                measurer.MeasureRushes();
                measurer.CountPaths();
                measurer.CheckReach(placer.Placements);
                measurer.MeasureBases(placer.Placements);
            }

            return measurer;
        }

        private bool FindSpawnCells()
        {
            foreach (var spawn in _layout.Spawns)
            {
                if (!_nav.TryFindWalkable(spawn.Position, _palette.baseRadius, out var cell))
                {
                    _issues.Error($"Slot {spawn.Slot}'s base at {spawn.Position} has no walkable ground.");
                    return false;
                }

                _spawnCells.Add(cell);
                Metrics.spawns.Add(new MapSpawnMetrics { slot = spawn.Slot, team = spawn.Team, rush = float.MaxValue });
            }

            return true;
        }

        private void MeasureRushes()
        {
            var spawns = _layout.Spawns;
            for (var a = 0; a < spawns.Count; a++)
            {
                for (var b = a + 1; b < spawns.Count; b++)
                {
                    if (spawns[a].Team == spawns[b].Team)
                    {
                        continue;
                    }

                    var path = MapPathfinder.Find(_nav, _spawnCells[a], _spawnCells[b]);
                    if (path == null)
                    {
                        _issues.Error($"No ground path from slot {spawns[a].Slot} to slot {spawns[b].Slot}.");
                        continue;
                    }

                    var seconds = path.Length / _rules.vehicleSpeed;
                    Metrics.spawns[a].rush = Mathf.Min(Metrics.spawns[a].rush, seconds);
                    Metrics.spawns[b].rush = Mathf.Min(Metrics.spawns[b].rush, seconds);
                    Metrics.maxExpansions = Mathf.Max(Metrics.maxExpansions, path.Expansions);
                }
            }

            Metrics.minRush = Metrics.spawns.Min(s => s.rush);
            Metrics.maxRush = Metrics.spawns.Max(s => s.rush);
            CheckRushes();
        }

        private void CheckRushes()
        {
            var mode = _rules.Mode(_layout.Spec.mode);
            foreach (var spawn in Metrics.spawns.Where(s => s.rush < mode.rushMin || s.rush > mode.rushMax))
            {
                _issues.Error($"Slot {spawn.slot} reaches its nearest enemy in {spawn.rush:0} s; {_layout.Spec.mode} wants {mode.rushMin:0} to {mode.rushMax:0} s.");
            }

            var copies = _layout.Symmetry.Copies.Count;
            for (var i = 0; i < Metrics.spawns.Count; i += copies)
            {
                var group = Metrics.spawns.Skip(i).Take(copies).Select(s => s.rush).ToList();
                if (group.Max() > group.Min() * (1f + FairnessTolerance))
                {
                    _issues.Error($"Symmetric slots {i + 1} to {i + copies} differ in rush time ({group.Min():0} to {group.Max():0} s).");
                }
            }

            if (Metrics.maxExpansions > NavGrid.MaxSearchExpansions)
            {
                _issues.Warn($"A base-to-base search expands {Metrics.maxExpansions} cells, over the runtime budget of {NavGrid.MaxSearchExpansions}; long moves may stop short.");
            }
        }

        private void CountPaths()
        {
            if (_spawnCells.Count < 2)
            {
                return;
            }

            var mode = _rules.Mode(_layout.Spec.mode);

            Paths = MapPathCounter.Count(_nav, _spawnCells[0], _spawnCells[1], _rules.corridorWidth,
                _palette.baseRadius, mode.minPaths + 3);
            Metrics.paths = Paths.Count;
            if (Paths.Count < mode.minPaths)
            {
                _issues.Error($"Slots 1 and 2 have {Paths.Count} separate attack paths; {_layout.Spec.mode} wants {mode.minPaths}.");
            }
        }

        private void CheckReach(List<MapPlacement> placements)
        {
            var reachable = MapPathCounter.Flood(_nav, _spawnCells[0]);
            foreach (var placement in placements.Where(p => p.Entry.IsGameplay))
            {
                var found = _nav.TryFindWalkable(placement.Position, placement.Radius + 4f, out var cell);
                if (!found || !reachable[_nav.Grid.Index(cell)])
                {
                    _issues.Error($"'{placement.Entry.key}' at {placement.Position} can't be reached by ground from slot 1.");
                }
            }
        }

        private void MeasureBases(List<MapPlacement> placements)
        {
            var radius = _palette.baseRadius;
            for (var i = 0; i < _layout.Spawns.Count; i++)
            {
                var spawn = _layout.Spawns[i];
                var metrics = Metrics.spawns[i];
                metrics.buildable = BuildableShare(spawn.Position, radius);
                if (metrics.buildable < _rules.minBuildableShare)
                {
                    _issues.Error($"Only {metrics.buildable:P0} of slot {spawn.Slot}'s base can be built on; the rules want {_rules.minBuildableShare:P0}.");
                }

                foreach (var node in placements.Where(p => (p.Position - spawn.Position).magnitude < radius * FieldReach))
                {
                    if (node.Entry.IsResource)
                    {
                        metrics.fields++;
                        metrics.supply += node.Amount;
                    }
                }
            }
        }

        private float BuildableShare(Vector2 center, float radius)
        {
            var open = 0;
            var total = 0;
            var reach = Mathf.CeilToInt(radius / _nav.Grid.CellSize);
            var middle = _nav.Cell(center);
            for (var y = -reach; y <= reach; y++)
            {
                for (var x = -reach; x <= reach; x++)
                {
                    var cell = middle + new int2(x, y);
                    if (math.length(new float2(x, y)) * _nav.Grid.CellSize > radius || !_nav.Grid.InBounds(cell))
                    {
                        continue;
                    }

                    total++;
                    open += _nav.IsWalkable(cell) ? 1 : 0;
                }
            }

            return total > 0 ? (float)open / total : 0f;
        }
    }
}
