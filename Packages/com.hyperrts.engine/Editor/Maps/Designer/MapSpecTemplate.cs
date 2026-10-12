using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using HyperRTS.Editor.Maps.Layout;
using HyperRTS.Editor.Maps.Spec;
using HyperRTS.Editor.Maps.Symmetry;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Designer
{
    /// <summary>
    /// A new map's starting spec: its header, flat-ish land with a margin and the spawns of one sector, which the
    /// symmetry copies into the others. Team modes take a two-sector symmetry (each sector is a team), FFA a
    /// rotate-N one (each sector is a player).
    /// </summary>
    public static class MapSpecTemplate
    {
        public const string Status = "blockout";
        public const string MapType = "land";

        private const float SpawnDepth = 0.35f;
        private const float SpawnSpread = 0.6f;
        private const float MarginShare = 0.3f;
        private const float TexelsPerMetre = 1.5f;
        private const int MinResolution = 513;
        private const int MaxRotations = 8;

        public static readonly IReadOnlyList<string> Modes = new[] { "1v1", "2v2", "3v3", "4v4", MapExpander.FreeForAll };

        public static readonly IReadOnlyList<string> Symmetries = new[]
            { MapSymmetry.Rotate180, MapSymmetry.MirrorXName, MapSymmetry.MirrorZName }
            .Concat(Enumerable.Range(3, MaxRotations - 2).Select(n => $"rotate-{n}")).ToArray();

        private static readonly Regex PascalCase = new("^[A-Z][A-Za-z0-9]*$");

        /// <summary>The spec of a new map; throws <see cref="ArgumentException"/> naming the field that doesn't fit.</summary>
        public static MapSpec Create(string name, string mode, Vector2Int size, string symmetryName, string climate,
            int seed)
        {
            RequireName(name);
            if (size.x <= 0 || size.y <= 0)
            {
                throw new ArgumentException($"Map size {size} must be positive.");
            }

            if (string.IsNullOrEmpty(climate))
            {
                throw new ArgumentException("Pick a climate for the map.");
            }

            var symmetry = MapSymmetry.Parse(symmetryName);
            var spec = new MapSpec
            {
                name = name,
                displayName = name,
                mode = mode,
                mapType = MapType,
                climate = climate,
                size = new MapPoint(size.x, size.y),
                symmetry = symmetryName,
                seed = seed,
                terrain = Terrain(size),
                status = Status,
            };
            spec.spawns.AddRange(Spawns(symmetry, spec.size.ToVector2(), SpawnsPerSector(mode, symmetry.Copies.Count)));
            return spec;
        }

        public static void RequireName(string name)
        {
            if (name == null || !PascalCase.IsMatch(name))
            {
                throw new ArgumentException($"Map name '{name}' must be PascalCase letters and digits (e.g. RedValley).");
            }
        }

        /// <summary>Gentle relief with water below the base height and a margin beyond the playable bounds.</summary>
        public static MapTerrainSpec Terrain(Vector2Int size)
        {
            var margin = MapSnap.Grid(Mathf.Max(size.x, size.y) * MarginShare);
            var extent = Mathf.Max(size.x, size.y) + margin * 2f;
            var resolution = Mathf.Max(MinResolution - 1, Mathf.NextPowerOfTwo(Mathf.CeilToInt(extent * TexelsPerMetre)));
            return new MapTerrainSpec
            {
                height = 20f,
                relief = 3f,
                reliefScale = 220f,
                waterLevel = 12f,
                maxHeight = 50f,
                resolution = resolution + 1,
                margin = margin,
            };
        }

        /// <summary>Spawns in the authored sector: one per player of a team, or one in FFA.</summary>
        public static int SpawnsPerSector(string mode, int copies)
        {
            if (!Modes.Contains(mode))
            {
                throw new ArgumentException($"Unknown mode '{mode}'. Use {string.Join(", ", Modes)}.");
            }

            if (mode == MapExpander.FreeForAll)
            {
                if (copies < 3)
                {
                    throw new ArgumentException("FFA needs a rotate-3 to rotate-8 symmetry: each sector is one player.");
                }

                return 1;
            }

            if (copies != 2)
            {
                throw new ArgumentException($"Mode {mode} needs rotate-180, mirror-x or mirror-z: each sector is one team.");
            }

            return mode[0] - '0';
        }

        private static IEnumerable<MapSpawnSpec> Spawns(MapSymmetry symmetry, Vector2 size, int count)
        {
            if (symmetry.Copies.Count > 2)
            {
                var wedge = 360f / symmetry.Copies.Count * 0.5f * Mathf.Deg2Rad;
                var radius = SpawnDepth * Mathf.Min(size.x, size.y);
                yield return Spawn(new Vector2(Mathf.Cos(wedge), Mathf.Sin(wedge)) * radius);
                yield break;
            }

            var westward = symmetry.Name == MapSymmetry.MirrorXName;
            var inward = westward ? Vector2.left : Vector2.down;
            var across = westward ? Vector2.up : Vector2.right;
            var depth = SpawnDepth * (westward ? size.x : size.y);
            var spacing = SpawnSpread * (westward ? size.y : size.x) / count;
            for (var i = 0; i < count; i++)
            {
                yield return Spawn(inward * depth + across * ((i - (count - 1) * 0.5f) * spacing));
            }
        }

        /// <summary>A spawn on the grid, facing the map centre.</summary>
        private static MapSpawnSpec Spawn(Vector2 position)
        {
            var snapped = MapSnap.Grid(position);
            var yaw = Mathf.Repeat(Mathf.Round(Mathf.Atan2(-snapped.x, -snapped.y) * Mathf.Rad2Deg), 360f);
            return new MapSpawnSpec { pos = new MapPoint(snapped.x, snapped.y), yaw = yaw };
        }
    }
}
