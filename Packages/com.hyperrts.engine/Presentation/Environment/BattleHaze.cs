using System;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace HyperRTS.Presentation.Environment
{
    /// <summary>
    /// The smoke hanging over a fight: a 128 by 128 grid over the map that drifts with the wind, spreads a little and
    /// halves every 90 s. Its <see cref="Texels"/> are the fog density texture, one plus the haze in every cell.
    /// </summary>
    public sealed class BattleHaze : IDisposable
    {
        public const int Cells = 128;

        /// <summary>Texture layers up the y axis; both hold the same haze, so the fog's own falloff shapes it.</summary>
        public const int Layers = 2;

        public const float HalfLife = 90f;

        /// <summary>Share of a cell's haze that evens out with its neighbours each second.</summary>
        public const float Spread = 0.2f;

        /// <summary>The thickest haze, in multiples of the weather's own fog density.</summary>
        public const float Most = 4f;

        private readonly float2 _min;
        private readonly int2 _roll;
        private NativeArray<float> _amounts;
        private NativeArray<float> _drifted;
        private NativeArray<half> _texels;

        /// <summary>A square grid of side <paramref name="side"/> metres from <paramref name="min"/>.</summary>
        public BattleHaze(float2 min, float side)
        {
            _min = min;
            Side = side;
            CellSize = side / Cells;
            // The fog samples the texture from world zero, repeating every side, so the map's cells shift round it.
            _roll = (int2)math.round(min / CellSize);
            _amounts = new NativeArray<float>(Cells * Cells, Allocator.Persistent);
            _drifted = new NativeArray<float>(Cells * Cells, Allocator.Persistent);
            _texels = new NativeArray<half>(Cells * Layers * Cells, Allocator.Persistent);
            // A zero step writes clear air: every texel at the weather's own density.
            Advance(0f, float2.zero);
        }

        public float Side { get; }

        public float CellSize { get; }

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
            var low = math.max(Cell(position - reach), 0);
            var high = math.min(Cell(position + reach), Cells - 1);
            for (var z = low.y; z <= high.y; z++)
            {
                for (var x = low.x; x <= high.x; x++)
                {
                    var offset = Centre(new int2(x, z)) - position;
                    var falloff = 1f - math.lengthsq(offset) / (reach * reach);
                    if (falloff <= 0f)
                    {
                        continue;
                    }

                    var index = x + z * Cells;
                    _amounts[index] = math.min(_amounts[index] + amount * falloff, Most);
                }
            }
        }

        /// <summary>The haze in the cell under <paramref name="position"/>; 0 off the grid.</summary>
        public float Amount(float2 position)
        {
            var cell = Cell(position);
            var onGrid = math.all(cell >= 0) && math.all(cell < Cells);
            return onGrid ? _amounts[cell.x + cell.y * Cells] : 0f;
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
                Texels = _texels,
                Shift = drift * deltaTime / CellSize,
                Share = Spread * deltaTime,
                Keep = math.exp2(-deltaTime / HalfLife),
                Roll = _roll,
            }.Run();
        }

        public void Dispose()
        {
            _amounts.Dispose();
            _drifted.Dispose();
            _texels.Dispose();
        }

        private int2 Cell(float2 position) => (int2)math.floor((position - _min) / CellSize);

        private float2 Centre(int2 cell) => _min + ((float2)cell + 0.5f) * CellSize;
    }
}
