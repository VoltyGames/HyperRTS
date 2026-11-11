using HyperRTS.Simulation.Common.Cells;
using System.Collections.Generic;
using HyperRTS.Simulation.Cues.Surfaces;
using HyperRTS.Presentation.Common;
using Unity.Mathematics;
using UnityEngine;

namespace HyperRTS.Presentation.Environment.Fire
{
    /// <summary>
    /// Grass and crop fires on a 1 m grid that spread downwind and burn out. Every random choice is hashed, so clients that
    /// see the same impacts burn the same fields.
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

        private static readonly int2[] Neighbours =
        {
            new(1, 0), new(-1, 0), new(0, 1), new(0, -1), new(1, 1), new(1, -1), new(-1, 1), new(-1, -1),
        };

        private readonly CellGrid _grid;
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
            _grid = new CellGrid(min, CellSize, size);
            _burnSeconds = burnSeconds;
            _reach = reach;
            _cells = new FieldCell[size.x * size.y];
        }

        public int2 Size => _grid.Size;

        public int Burning => _flames.Count;

        /// <summary>The middle of each cell that burnt out during the last <see cref="Advance"/>.</summary>
        public IReadOnlyList<float2> BurntOut => _burntOut;

        /// <summary>Marks dry every cell whose terrain texel <paramref name="dry"/> flags and that isn't water.</summary>
        public void MarkDryTerrain(Terrain terrain, bool[] dry, in GroundMaterials ground)
        {
            var data = terrain.terrainData;
            var resolution = data.alphamapResolution;
            var origin = ((float3)terrain.GetPosition()).xz;
            var perMetre = resolution / ((float3)data.size).xz;
            foreach (var cell in _grid.Cells(int2.zero, Size - 1))
            {
                var centre = _grid.Center(cell);
                var texel = (int2)math.floor((centre - origin) * perMetre);
                var onTerrain = math.all(texel >= 0) && math.all(texel < resolution);
                if (onTerrain && dry[texel.x + texel.y * resolution] && ground.At(centre) != SurfaceMaterial.Water)
                {
                    MarkDry(cell);
                }
            }
        }

        public void MarkDry(int2 cell) => _cells[_grid.Index(cell)] = FieldCell.Dry;

        /// <summary>Makes the dry cells within <paramref name="radius"/> of <paramref name="centre"/> bare for good.</summary>
        public void MarkBare(float2 centre, float radius)
        {
            foreach (var cell in _grid.CellsInCircle(centre, radius))
            {
                var index = _grid.Index(cell);
                if (_cells[index] == FieldCell.Dry)
                {
                    _cells[index] = FieldCell.Bare;
                }
            }
        }

        /// <summary>The cell under <paramref name="position"/>; bare off the grid.</summary>
        public FieldCell At(float2 position)
        {
            var cell = _grid.Cell(position);
            return _grid.Contains(cell) ? _cells[_grid.Index(cell)] : FieldCell.Bare;
        }

        public float2 Centre(int2 cell) => _grid.Center(cell);

        /// <summary>Sets the dry cell under <paramref name="position"/> alight; false on any other cell.</summary>
        public bool TryIgnite(float2 position, uint seed)
        {
            var cell = _grid.Cell(position);
            if (!_grid.Contains(cell) || _cells[_grid.Index(cell)] != FieldCell.Dry)
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
                var cell = _grid.Cell(flame.Cell);
                var key = cell.x / ClusterCells + cell.y / ClusterCells * across;
                _clusterSums.TryGetValue(key, out var sum);
                _clusterSums[key] = sum + new float3(_grid.Center(cell), 1f);
            }

            foreach (var (key, sum) in _clusterSums)
            {
                clusters.Add(new FieldFireCluster { Key = key, Centre = sum.xy / sum.z, Cells = (int)sum.z });
            }
        }

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
                _burntOut.Add(_grid.Center(_grid.Cell(flame.Cell)));
                _flames[i] = _flames[^1];
                _flames.RemoveAt(_flames.Count - 1);
            }

            _flames.AddRange(_lit);
            _lit.Clear();
        }

        // Lights some dry neighbours; false once none is left within reach, as cells never turn dry again.
        private bool Spread(in Flame flame)
        {
            var cell = _grid.Cell(flame.Cell);
            var anyDry = false;
            for (var n = 0; n < Neighbours.Length; n++)
            {
                var next = cell + Neighbours[n];
                if (!_grid.Contains(next) || _cells[_grid.Index(next)] != FieldCell.Dry)
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
            Hashes.Fraction(math.hash(new uint3(seed, (uint)cell, salt)));

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
