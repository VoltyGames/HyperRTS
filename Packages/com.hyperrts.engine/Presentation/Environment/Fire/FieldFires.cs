using System;
using System.Collections.Generic;
using HyperRTS.Simulation.Cues;
using Unity.Mathematics;
using UnityEngine;

namespace HyperRTS.Presentation.Environment.Fire
{
    /// <summary>
    /// Grass and crop fires on a 1 m grid over the map. Fronts spread cell to cell, faster downwind, and less likely the
    /// nearer they get to their ignition's reach, so each shell leaves a blotch. Each cell burns for a while and stays
    /// burnt, and fires stop at bare cells. Every random choice hashes the lighting impact's seed, the
    /// cell and the burning cell's age, so clients that see the same impacts burn closely the same fields.
    /// </summary>
    public sealed class FieldFires
    {
        public const float CellSize = 1f;
        public const float StepSeconds = 0.25f;

        /// <summary>Cells along a side of the squares the burning cells are gathered in for effects.</summary>
        public const int ClusterCells = 8;

        /// <summary>Chance per second that a burning cell lights a dry neighbour in still air.</summary>
        public const float SpreadPerSecond = 0.25f;

        /// <summary>Spread added per m/s of wind blowing towards the neighbour, and taken away against it.</summary>
        public const float WindBias = 1.5f;

        /// <summary>Cells burning at once beyond which fronts wait, so a burning map stays inside its CPU budget.</summary>
        public const int MaxBurning = 8192;

        // Share of still-air spread a front keeps when backing into the wind.
        private const float LeastSpread = 0.1f;

        // Spread odds fall off as the cube of the share of the reach left, so fronts thin out and die well inside it.
        private const float ReachFalloff = 3f;
        private const uint BurnSalt = 0x9E3779B9u;
        private const uint ReachSalt = 0x85EBCA6Bu;

        // Rows of the alphamap read at once, so a large terrain never copies all its weights in one go.
        private const int AlphamapRows = 64;

        private static readonly int2[] Neighbours =
        {
            new(1, 0), new(-1, 0), new(0, 1), new(0, -1), new(1, 1), new(1, -1), new(-1, 1), new(-1, -1),
        };

        private readonly GridArea _grid;
        private readonly float2 _burnSeconds;
        private readonly float2 _reach;
        private readonly FieldCell[] _cells;
        private readonly float[] _chances = new float[8];
        private readonly List<Flame> _flames = new();
        private readonly List<Flame> _lit = new();
        private readonly List<float2> _burntOut = new();
        private readonly Dictionary<int, float3> _clusterSums = new();
        private float _carry;

        /// <summary>
        /// A grid of <paramref name="size"/> cells from <paramref name="min"/>, all bare until marked dry. Each ignition
        /// spreads at most a <paramref name="reach"/> (metres, min and max) from where it started.
        /// </summary>
        public FieldFires(float2 min, int2 size, float2 burnSeconds, float2 reach)
        {
            _grid = new GridArea(min, CellSize, size);
            _burnSeconds = burnSeconds;
            _reach = reach;
            _cells = new FieldCell[size.x * size.y];
        }

        public int2 Size => _grid.Size;

        public int Burning => _flames.Count;

        /// <summary>The middle of each cell that burnt out during the last <see cref="Advance"/>.</summary>
        public IReadOnlyList<float2> BurntOut => _burntOut;

        public void MarkDry(int2 cell) => _cells[_grid.Index(cell)] = FieldCell.Dry;

        /// <summary>Makes the dry cells within <paramref name="radius"/> of <paramref name="centre"/> bare for good.</summary>
        public void MarkBare(float2 centre, float radius)
        {
            var low = math.max(_grid.Cell(centre - radius), 0);
            var high = math.min(_grid.Cell(centre + radius), Size - 1);
            for (var z = low.y; z <= high.y; z++)
            {
                for (var x = low.x; x <= high.x; x++)
                {
                    var index = _grid.Index(new int2(x, z));
                    var near = math.distancesq(_grid.Centre(new int2(x, z)), centre) <= radius * radius;
                    if (near && _cells[index] == FieldCell.Dry)
                    {
                        _cells[index] = FieldCell.Bare;
                    }
                }
            }
        }

        /// <summary>The cell under <paramref name="position"/>; bare off the grid.</summary>
        public FieldCell At(float2 position)
        {
            var cell = _grid.Cell(position);
            return _grid.Inside(cell) ? _cells[_grid.Index(cell)] : FieldCell.Bare;
        }

        public float2 Centre(int2 cell) => _grid.Centre(cell);

        /// <summary>Sets the dry cell under <paramref name="position"/> alight; false on any other cell.</summary>
        public bool TryIgnite(float2 position, uint seed)
        {
            var cell = _grid.Cell(position);
            if (!_grid.Inside(cell) || _cells[_grid.Index(cell)] != FieldCell.Dry)
            {
                return false;
            }

            var index = _grid.Index(cell);
            var reach = math.lerp(_reach.x, _reach.y, Fraction(seed, index, ReachSalt)) / CellSize;
            Light(index, seed, cell, reach, _flames);
            return true;
        }

        /// <summary>
        /// Burns on <paramref name="deltaTime"/> seconds in fixed steps under a downwind <paramref name="wind"/> in m/s.
        /// Returns the steps taken; <see cref="BurntOut"/> holds what they burnt out.
        /// </summary>
        public int Advance(float deltaTime, float2 wind)
        {
            _burntOut.Clear();
            if (_flames.Count == 0)
            {
                _carry = 0f;
                return 0;
            }

            var steps = 0;
            for (_carry += deltaTime; _carry >= StepSeconds; _carry -= StepSeconds)
            {
                Step(wind);
                steps++;
            }

            return steps;
        }

        /// <summary>Fills <paramref name="clusters"/> with the burning cells gathered per cluster square.</summary>
        public void GatherClusters(List<FieldFireCluster> clusters)
        {
            clusters.Clear();
            _clusterSums.Clear();
            var across = (Size.x + ClusterCells - 1) / ClusterCells;
            foreach (var flame in _flames)
            {
                var cell = _grid.FromIndex(flame.Cell);
                var key = cell.x / ClusterCells + cell.y / ClusterCells * across;
                _clusterSums.TryGetValue(key, out var sum);
                _clusterSums[key] = sum + new float3(_grid.Centre(cell), 1f);
            }

            foreach (var (key, sum) in _clusterSums)
            {
                clusters.Add(new FieldFireCluster { Key = key, Centre = sum.xy / sum.z, Cells = (int)sum.z });
            }
        }

        /// <summary>Marks dry every cell whose terrain is mostly <paramref name="dryLayers"/> and isn't water.</summary>
        public void MarkDryTerrain(Terrain terrain, TerrainLayer[] dryLayers, in GroundMaterials ground)
        {
            var data = terrain.terrainData;
            var resolution = data.alphamapResolution;
            var dry = DryTexels(data, dryLayers);
            var origin = ((float3)terrain.GetPosition()).xz;
            var perMetre = resolution / ((float3)data.size).xz;
            for (var z = 0; z < Size.y; z++)
            {
                for (var x = 0; x < Size.x; x++)
                {
                    var centre = _grid.Centre(new int2(x, z));
                    var texel = (int2)math.floor((centre - origin) * perMetre);
                    var onTerrain = math.all(texel >= 0) && math.all(texel < resolution);
                    if (onTerrain && dry[texel.x + texel.y * resolution] && ground.At(centre) != SurfaceMaterial.Water)
                    {
                        MarkDry(new int2(x, z));
                    }
                }
            }
        }

        /// <summary>A 32-bit hash as a number from 0 up to but not including 1.</summary>
        public static float HashFraction(uint hash) => (hash >> 8) * (1f / (1 << 24));

        /// <summary>Puts every fire out and makes burnt ground dry again: a replay sought back past the fires.</summary>
        public void Clear()
        {
            for (var i = 0; i < _cells.Length; i++)
            {
                if (_cells[i] != FieldCell.Bare)
                {
                    _cells[i] = FieldCell.Dry;
                }
            }

            _flames.Clear();
            _burntOut.Clear();
            _carry = 0f;
        }

        // Whether each alphamap texel is over half dry layers, row-major from the terrain's south-west corner.
        private static bool[] DryTexels(TerrainData data, TerrainLayer[] dryLayers)
        {
            var resolution = data.alphamapResolution;
            var burns = Array.ConvertAll(data.terrainLayers, layer => Array.IndexOf(dryLayers, layer) >= 0);
            var dry = new bool[resolution * resolution];
            for (var row = 0; row < resolution; row += AlphamapRows)
            {
                var rows = Math.Min(AlphamapRows, resolution - row);
                var weights = data.GetAlphamaps(0, row, resolution, rows);
                for (var z = 0; z < rows; z++)
                {
                    for (var x = 0; x < resolution; x++)
                    {
                        var share = 0f;
                        for (var layer = 0; layer < burns.Length; layer++)
                        {
                            share += burns[layer] ? weights[z, x, layer] : 0f;
                        }

                        dry[x + (row + z) * resolution] = share > 0.5f;
                    }
                }
            }

            return dry;
        }

        private void Step(float2 wind)
        {
            for (var n = 0; n < Neighbours.Length; n++)
            {
                var offset = (float2)Neighbours[n];
                var share = math.max(LeastSpread, 1f + WindBias * math.dot(math.normalize(offset), wind));
                _chances[n] = SpreadPerSecond * StepSeconds * share / math.length(offset);
            }

            for (var i = _flames.Count - 1; i >= 0; i--)
            {
                var flame = _flames[i];
                flame.Age++;
                flame.Enclosed = flame.Enclosed || !Spread(flame);
                flame.Left -= StepSeconds;
                if (flame.Left > 0f)
                {
                    _flames[i] = flame;
                    continue;
                }

                _cells[flame.Cell] = FieldCell.Burnt;
                _burntOut.Add(_grid.Centre(_grid.FromIndex(flame.Cell)));
                _flames[i] = _flames[^1];
                _flames.RemoveAt(_flames.Count - 1);
            }

            _flames.AddRange(_lit);
            _lit.Clear();
        }

        // Lights some dry neighbours; false once none is left within reach, as cells never turn dry again.
        private bool Spread(in Flame flame)
        {
            var cell = _grid.FromIndex(flame.Cell);
            var anyDry = false;
            for (var n = 0; n < Neighbours.Length; n++)
            {
                var next = cell + Neighbours[n];
                if (!_grid.Inside(next) || _cells[_grid.Index(next)] != FieldCell.Dry)
                {
                    continue;
                }

                var left = 1f - math.distance((float2)next, flame.Origin) / flame.Reach;
                if (left <= 0f)
                {
                    continue;
                }

                anyDry = true;
                var index = _grid.Index(next);
                var chance = _chances[n] * math.pow(left, ReachFalloff);
                if (Fraction(flame.Seed, index, flame.Age * 8u + (uint)n) < chance)
                {
                    Light(index, flame.Seed, flame.Origin, flame.Reach, _lit);
                }
            }

            return anyDry;
        }

        private void Light(int index, uint seed, int2 origin, float reach, List<Flame> into)
        {
            if (_flames.Count + _lit.Count >= MaxBurning)
            {
                return;
            }

            _cells[index] = FieldCell.Burning;
            var seconds = math.lerp(_burnSeconds.x, _burnSeconds.y, Fraction(seed, index, BurnSalt));
            into.Add(new Flame { Cell = index, Seed = seed, Origin = origin, Reach = reach, Left = seconds });
        }

        private static float Fraction(uint seed, int cell, uint salt) =>
            HashFraction(math.hash(new uint3(seed, (uint)cell, salt)));

        private struct Flame
        {
            public int Cell;
            public uint Seed;

            /// <summary>The cell its fire was lit in, and how many cells from it the fire may spread.</summary>
            public int2 Origin;
            public float Reach;
            public float Left;
            public uint Age;

            /// <summary>No dry neighbour is left, so it stops looking.</summary>
            public bool Enclosed;
        }
    }
}
