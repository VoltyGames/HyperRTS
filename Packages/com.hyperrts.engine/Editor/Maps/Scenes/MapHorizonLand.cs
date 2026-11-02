using System.Collections.Generic;
using System.Linq;
using HyperRTS.Editor.Maps.Build;
using HyperRTS.Editor.Maps.Common;
using HyperRTS.Editor.Maps.Heights;
using HyperRTS.Editor.Maps.Spec;
using Unity.Mathematics;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Scenes
{
    /// <summary>
    /// What the horizon tiles hold: heights that carry on from the map's edge into open relief, the farmland kit's
    /// crops as large fields with straight margins, and belts of its trees along some field edges.
    /// </summary>
    internal sealed class MapHorizonLand
    {
        /// <summary>Metres over which the map's edge heights give way to the open relief.</summary>
        private const float Blend = 160f;

        /// <summary>How far below the map's ground a tile lies where it reaches under the map, so no crack shows.</summary>
        private const float TuckDepth = 0.6f;

        /// <summary>Horizon fields are this many times the kit's field size, since they are only seen far off.</summary>
        private const float FieldScale = 1.6f;

        /// <summary>Share of field edges that carry a tree belt.</summary>
        private const float BeltShare = 0.35f;

        private readonly MapHeights _heights;
        private readonly MapTerrainSpec _terrain;
        private readonly Rect _map;
        private readonly uint _seed;
        private readonly Vector2 _fieldSize;
        private readonly Quaternion _toGrid;
        private readonly Quaternion _fromGrid;
        private readonly float _margin;
        private readonly int _marginLayer;
        private readonly (int Layer, float Weight)[] _crops;
        private readonly (int First, int Variants, float Weight)[] _beltTrees;
        private readonly float _beltSpacing;
        private readonly int _layers;

        public MapHorizonLand(MapGeneration generation)
        {
            var (spec, climate) = (generation.Project.Spec, generation.Project.Climate);
            _heights = generation.Heights;
            _terrain = generation.Layout.Terrain;
            _map = generation.Layout.TerrainBounds;
            _seed = (uint)spec.seed;
            _layers = climate.layers.Count;
            var farmland = spec.farmland.Count > 0 ? spec.farmland[0] : null;
            var kit = climate.FarmlandKit(farmland != null ? farmland.kind : climate.farmlandKitKeys[0]);
            var angle = farmland?.angle ?? 0f;
            _toGrid = Quaternion.Euler(0f, 0f, -angle);
            _fromGrid = Quaternion.Euler(0f, 0f, angle);
            _fieldSize = kit.fieldSize * FieldScale;
            _margin = kit.marginWidth * 2f;
            _marginLayer = climate.SurfaceLayer(kit.marginSurface);
            _crops = kit.crops.Select(crop => (climate.SurfaceLayer(crop.surface), crop.weight)).ToArray();
            TreePrototypes = climate.TreePrototypes(out var firsts).ToArray();
            var belt = climate.BeltKit(kit.beltKit);
            _beltSpacing = belt.spacing;
            _beltTrees = belt.trees.Select(share =>
            {
                var species = climate.TreeIndex(share.tree);
                return (firsts[species], climate.trees[species].prefabs.Count, share.weight);
            }).ToArray();
        }

        /// <summary>One prototype per species and variant, each species' variants in a run.</summary>
        public TreePrototype[] TreePrototypes { get; }

        /// <summary>Normalised heights over <paramref name="rect"/>, [z, x], as TerrainData takes them.</summary>
        public float[,] Heights(Rect rect, int resolution)
        {
            var samples = new float[resolution, resolution];
            var inside = Rect.MinMaxRect(_map.xMin + 1f, _map.yMin + 1f, _map.xMax - 1f, _map.yMax - 1f);
            for (var z = 0; z < resolution; z++)
            {
                for (var x = 0; x < resolution; x++)
                {
                    var p = rect.min + Vector2.Scale(rect.size, new Vector2(x, z) / (resolution - 1f));
                    samples[z, x] = Mathf.Clamp01(Height(p, inside) / _terrain.maxHeight);
                }
            }

            return samples;
        }

        private float Height(Vector2 p, Rect inside)
        {
            var edge = Vector2.Max(inside.min, Vector2.Min(inside.max, p));
            var mapHeight = _heights.Height(edge);
            if (_map.Contains(p))
            {
                return mapHeight - TuckDepth;
            }

            var relief = (Mathf.PerlinNoise((p.x + _seed % 997) / _terrain.reliefScale, p.y / _terrain.reliefScale) * 2f - 1f)
                         * _terrain.relief;
            var open = _terrain.height + relief;
            return Mathf.Lerp(mapHeight, open, Mathf.SmoothStep(0f, 1f, (p - edge).magnitude / Blend));
        }

        /// <summary>Layer weights over <paramref name="rect"/>, [z, x, layer], one layer per texel.</summary>
        public float[,,] Weights(Rect rect, int resolution)
        {
            var weights = new float[resolution, resolution, _layers];
            for (var z = 0; z < resolution; z++)
            {
                for (var x = 0; x < resolution; x++)
                {
                    var p = rect.min + Vector2.Scale(rect.size, new Vector2(x + 0.5f, z + 0.5f) / resolution);
                    weights[z, x, Layer(p)] = 1f;
                }
            }

            return weights;
        }

        private int Layer(Vector2 p)
        {
            var (cell, local) = Field(p);
            var nearEdge = Mathf.Min(Mathf.Min(local.x, _fieldSize.x - local.x), Mathf.Min(local.y, _fieldSize.y - local.y));
            return nearEdge < _margin ? _marginLayer : MapRandom.Pick(_crops, crop => crop.Weight, Hash(cell.x, cell.y, 1)).Layer;
        }

        // The field cell a point falls in and the point's place in it, rows of fields staggered by a random offset.
        private (int2 Cell, Vector2 Local) Field(Vector2 p)
        {
            Vector2 grid = _toGrid * p;
            var row = Mathf.FloorToInt(grid.y / _fieldSize.y);
            var shifted = grid.x + RowShift(row);
            var column = Mathf.FloorToInt(shifted / _fieldSize.x);
            var local = new Vector2(shifted - column * _fieldSize.x, grid.y - row * _fieldSize.y);
            return (new int2(column, row), local);
        }

        /// <summary>Belt trees on the field edges inside <paramref name="rect"/> and outside the map.</summary>
        public List<TreeInstance> Trees(Rect rect)
        {
            var trees = new List<TreeInstance>();
            if (_beltTrees.Length == 0)
            {
                return trees;
            }

            // Walk the grid's rows and their field ends; every tree is checked against the tile, so overlap is harmless.
            var reach = (rect.size.magnitude + _map.size.magnitude) * 0.5f + rect.center.magnitude;
            var rows = Mathf.CeilToInt(reach / _fieldSize.y);
            var columns = Mathf.CeilToInt(reach / _fieldSize.x) + 1;
            for (var row = -rows; row <= rows; row++)
            {
                for (var column = -columns; column <= columns; column++)
                {
                    var origin = new Vector2(column * _fieldSize.x - RowShift(row), row * _fieldSize.y);
                    TryBelt(trees, rect, origin, Vector2.right * _fieldSize.x, Hash(column, row, 3));
                    TryBelt(trees, rect, origin, Vector2.up * _fieldSize.y, Hash(column, row, 4));
                }
            }

            return trees;
        }

        private void TryBelt(List<TreeInstance> trees, Rect rect, Vector2 gridStart, Vector2 gridRun, float chance)
        {
            if (chance >= BeltShare)
            {
                return;
            }

            var count = Mathf.FloorToInt(gridRun.magnitude / _beltSpacing);
            for (var i = 0; i < count; i++)
            {
                Vector2 p = _fromGrid * (gridStart + gridRun * ((i + 0.5f) / count));
                if (!rect.Contains(p) || _map.Contains(p))
                {
                    continue;
                }

                var spot = new int2(Mathf.RoundToInt(p.x), Mathf.RoundToInt(p.y));
                var pick = Hash(spot.x, spot.y, 5);
                var (first, variants, _) = MapRandom.Pick(_beltTrees, tree => tree.Weight, pick);
                var variant = Mathf.Min(variants - 1, (int)(pick * 7919f) % variants);
                var scale = Mathf.Lerp(0.85f, 1.15f, Hash(spot.x, spot.y, 6));
                trees.Add(MapTerrainWriter.Instance(first + variant, p, pick * 360f, scale, rect));
            }
        }

        // Rows of fields are staggered by a random share of a field's width.
        private float RowShift(int row) => Hash(row, 0, 2) * _fieldSize.x;

        private float Hash(int a, int b, int salt) => math.hash(new int4(a, b, salt, (int)_seed)) / (float)uint.MaxValue;
    }
}
