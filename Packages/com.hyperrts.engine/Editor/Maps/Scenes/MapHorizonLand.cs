using System.Collections.Generic;
using HyperRTS.Editor.Maps.Build;
using HyperRTS.Editor.Maps.Climate;
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
        private readonly List<(int Layer, float Weight)> _crops = new();
        private readonly List<(int First, int Variants, float Weight)> _beltTrees = new();
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
            kit.crops.ForEach(crop => _crops.Add((climate.SurfaceLayer(crop.surface), crop.weight)));
            TreePrototypes = Prototypes(climate, out var firsts);
            var belt = climate.BeltKit(kit.beltKit);
            _beltSpacing = belt.spacing;
            foreach (var share in belt.trees)
            {
                var species = climate.TreeIndex(share.tree);
                _beltTrees.Add((firsts[species], climate.trees[species].prefabs.Count, share.weight));
            }
        }

        /// <summary>One prototype per species and variant, each species' variants in a run.</summary>
        public TreePrototype[] TreePrototypes { get; }

        private static TreePrototype[] Prototypes(MapClimate climate, out int[] firsts)
        {
            var prototypes = new List<TreePrototype>();
            firsts = new int[climate.trees.Count];
            for (var i = 0; i < climate.trees.Count; i++)
            {
                firsts[i] = prototypes.Count;
                climate.trees[i].prefabs.ForEach(prefab => prototypes.Add(new TreePrototype { prefab = prefab }));
            }

            return prototypes.ToArray();
        }

        /// <summary>Normalised heights over <paramref name="rect"/>, [z, x], as TerrainData takes them.</summary>
        public float[,] Heights(Rect rect, int resolution, float maxHeight, Rect map)
        {
            var samples = new float[resolution, resolution];
            var inside = Rect.MinMaxRect(map.xMin + 1f, map.yMin + 1f, map.xMax - 1f, map.yMax - 1f);
            for (var z = 0; z < resolution; z++)
            {
                for (var x = 0; x < resolution; x++)
                {
                    var p = rect.min + Vector2.Scale(rect.size, new Vector2(x, z) / (resolution - 1f));
                    samples[z, x] = Mathf.Clamp01(Height(p, inside) / maxHeight);
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
            return nearEdge < _margin ? _marginLayer : Pick(_crops, Hash(cell.x, cell.y, 1));
        }

        // The field cell a point falls in and the point's place in it, rows of fields staggered by a random offset.
        private (int2 Cell, Vector2 Local) Field(Vector2 p)
        {
            Vector2 grid = _toGrid * p;
            var row = Mathf.FloorToInt(grid.y / _fieldSize.y);
            var shifted = grid.x + Hash(row, 0, 2) * _fieldSize.x;
            var column = Mathf.FloorToInt(shifted / _fieldSize.x);
            var local = new Vector2(shifted - column * _fieldSize.x, grid.y - row * _fieldSize.y);
            return (new int2(column, row), local);
        }

        /// <summary>Belt trees on the field edges inside <paramref name="rect"/> and outside the map.</summary>
        public List<TreeInstance> Trees(Rect rect)
        {
            var trees = new List<TreeInstance>();
            if (_beltTrees.Count == 0)
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
                    var origin = new Vector2(column * _fieldSize.x - Hash(row, 0, 2) * _fieldSize.x, row * _fieldSize.y);
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

                var pick = Hash(Mathf.RoundToInt(p.x), Mathf.RoundToInt(p.y), 5);
                var (first, variants, _) = _beltTrees[PickIndex(_beltTrees.ConvertAll(t => t.Weight), pick)];
                var scale = Mathf.Lerp(0.85f, 1.15f, Hash(Mathf.RoundToInt(p.x), Mathf.RoundToInt(p.y), 6));
                var local = (p - rect.min) / rect.size;
                trees.Add(new TreeInstance
                {
                    prototypeIndex = first + Mathf.Min(variants - 1, (int)(pick * 7919f) % variants),
                    position = new Vector3(local.x, 0f, local.y),
                    widthScale = scale,
                    heightScale = scale,
                    rotation = pick * Mathf.PI * 2f,
                    color = Color.white,
                    lightmapColor = Color.white,
                });
            }
        }

        private static int Pick(List<(int Layer, float Weight)> choices, float pick) =>
            choices[PickIndex(choices.ConvertAll(c => c.Weight), pick)].Layer;

        private static int PickIndex(List<float> weights, float pick)
        {
            var total = 0f;
            weights.ForEach(w => total += w);
            var at = pick * total;
            for (var i = 0; i < weights.Count; i++)
            {
                at -= weights[i];
                if (at < 0f)
                {
                    return i;
                }
            }

            return weights.Count - 1;
        }

        private float Hash(int a, int b, int salt) => math.hash(new int4(a, b, salt, (int)_seed)) / (float)uint.MaxValue;
    }
}
