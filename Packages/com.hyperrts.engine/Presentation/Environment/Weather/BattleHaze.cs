using HyperRTS.Simulation.Common.Cells;
using HyperRTS.Simulation.Navigation.Heights;
using System;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace HyperRTS.Presentation.Environment.Weather
{
    /// <summary>
    /// The smoke hanging over a fight: a 128 by 128 grid over the map that drifts with the wind, spreads a little and
    /// halves every 90 s. Its <see cref="Texels"/> are the fog density texture: one plus the haze in every cell, plus
    /// the ground mist lying low in hollows and by water.
    /// </summary>
    public sealed class BattleHaze : IDisposable
    {
        public const int Cells = 128;

        /// <summary>Texture layers up the y axis without mist; both hold the same haze, so the fog's own falloff shapes it.</summary>
        public const int PlainLayers = 2;

        /// <summary>Metres one layer spans when mist is on, so mist a few metres deep keeps its height.</summary>
        public const float MistLayerHeight = 4f;

        public const int MistMostLayers = 192;

        /// <summary>
        /// Mist's extinction per metre at full strength and pooling; its own scale, since the weather's fog (about
        /// 0.001) is too thin for a few metres of mist to show.
        /// </summary>
        public const float MistMost = 0.12f;

        /// <summary>Metres of mist above the ground.</summary>
        public const float MistDepth = 6f;

        public const float HalfLife = 90f;

        /// <summary>Share of a cell's haze that evens out with its neighbours each second.</summary>
        public const float Spread = 0.2f;

        /// <summary>The thickest haze, in multiples of the weather's own fog density.</summary>
        public const float Most = 2f;

        private const int TexelBatch = 4096;

        private readonly CellGrid _grid;
        private readonly int2 _roll;
        private readonly float _mistExtinction;
        private MistField _mist;
        private NativeArray<float> _amounts;
        private NativeArray<float> _drifted;
        private NativeArray<half> _texels;

        /// <summary>
        /// A square grid of side <paramref name="side"/> metres from <paramref name="min"/>. A <paramref name="mist"/>
        /// above 0 (up to 1) makes the texture tall enough to hold ground mist; <see cref="LayMist"/> fills it.
        /// </summary>
        public BattleHaze(float2 min, float side, float mist, FogShape fog)
        {
            Fog = fog;
            Side = side;
            CellSize = side / Cells;
            Layers = mist > 0f
                ? math.clamp((int)math.ceil(side / MistLayerHeight), PlainLayers, MistMostLayers)
                : PlainLayers;
            _mistExtinction = mist * MistMost;
            _grid = new CellGrid(min, CellSize, new int2(Cells, Cells));
            // The fog samples the texture from world zero, repeating every side, so the map's cells shift round it.
            _roll = (int2)math.round(min / CellSize);
            _mist = new MistField(Cells * Cells);
            _amounts = new NativeArray<float>(Cells * Cells, Allocator.Persistent);
            _drifted = new NativeArray<float>(Cells * Cells, Allocator.Persistent);
            _texels = new NativeArray<half>(Cells * Layers * Cells, Allocator.Persistent);
            // A zero step writes clear air: every texel at the weather's own density.
            Advance(0f, float2.zero);
        }

        public float Side { get; }

        public float CellSize { get; }

        /// <summary>Texture layers up the y axis.</summary>
        public int Layers { get; }

        /// <summary>The weather's fog now; the system refreshes it so mist keeps its own density as the weather changes.</summary>
        public FogShape Fog { get; set; }

        /// <summary>Repeats of the density texture per metre, so one repeat spans the grid.</summary>
        public float Tiling => 1f / Side;

        /// <summary>The fog density texture's R16F texels, x fastest, then the layers, then z.</summary>
        public NativeArray<half> Texels => _texels;

        /// <summary>
        /// Thickens the haze by <paramref name="amount"/> at <paramref name="position"/>, fading out to
        /// <paramref name="radius"/> metres. Fires, impacts, burning wrecks and collapses all feed it here.
        /// </summary>
        public void Add(float2 position, float radius, float amount)
        {
            var reach = math.max(radius, CellSize);
            foreach (var cell in _grid.Cells(_grid.Cell(position - reach), _grid.Cell(position + reach)))
            {
                var offset = _grid.Center(cell) - position;
                var falloff = 1f - math.lengthsq(offset) / (reach * reach);
                if (falloff <= 0f)
                {
                    continue;
                }

                var index = _grid.Index(cell);
                _amounts[index] = math.min(_amounts[index] + amount * falloff, Most);
            }
        }

        /// <summary>
        /// Lays the mist from the terrain: cells lower than their surroundings, and cells by water, get more. It stays
        /// put while the haze drifts.
        /// </summary>
        public void LayMist(in TerrainHeight terrain, float waterLevel)
        {
            _mist.Lay(_grid, terrain, waterLevel);
            Advance(0f, float2.zero);
        }

        /// <summary>The mist's extinction per metre at world <paramref name="height"/> over <paramref name="position"/>.</summary>
        public float Mist(float2 position, float height)
        {
            var cell = _grid.Cell(position);
            if (!_grid.Contains(cell))
            {
                return 0f;
            }

            var index = _grid.Index(cell);
            var above = height - _mist.Ground[index];
            return _mist.Pooling[index] * _mistExtinction * math.saturate(1f - above / MistDepth);
        }

        /// <summary>The haze in the cell under <paramref name="position"/>; 0 off the grid.</summary>
        public float Amount(float2 position)
        {
            var cell = _grid.Cell(position);
            return _grid.Contains(cell) ? _amounts[_grid.Index(cell)] : 0f;
        }

        /// <summary>All the haze on the grid, in cell units.</summary>
        public float Total()
        {
            var total = 0f;
            foreach (var amount in _amounts)
            {
                total += amount;
            }

            return total;
        }

        /// <summary>
        /// Drifts the haze <paramref name="deltaTime"/> seconds down a wind of <paramref name="drift"/> m/s, spreads
        /// and thins it, and rewrites the <see cref="Texels"/>.
        /// </summary>
        public void Advance(float deltaTime, float2 drift)
        {
            new BattleHazeStepJob
            {
                Amounts = _amounts,
                Drifted = _drifted,
                Shift = drift * deltaTime / CellSize,
                Share = Spread * deltaTime,
                Keep = math.exp2(-deltaTime / HalfLife),
            }.Run();
            new BattleHazeTexelJob
            {
                Amounts = _amounts,
                Ground = _mist.Ground,
                Pooling = _mist.Pooling,
                Texels = _texels,
                Roll = _roll,
                Layers = Layers,
                Side = Side,
                MistExtinction = _mistExtinction,
                Fog = Fog,
            }.Schedule(_texels.Length, TexelBatch).Complete();
        }

        public void Dispose()
        {
            _mist.Dispose();
            _amounts.Dispose();
            _drifted.Dispose();
            _texels.Dispose();
        }
    }
}
