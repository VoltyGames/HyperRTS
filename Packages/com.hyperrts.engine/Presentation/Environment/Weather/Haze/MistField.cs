using HyperRTS.Simulation.Common.Cells;
using HyperRTS.Simulation.Navigation.Heights;
using System;
using Unity.Collections;
using Unity.Mathematics;

namespace HyperRTS.Presentation.Environment.Weather.Haze
{
    /// <summary>
    /// Where ground mist pools, per <see cref="BattleHaze"/> cell: how low the cell lies against its surroundings or
    /// the water, and the height the mist rests on.
    /// </summary>
    public struct MistField : IDisposable
    {
        /// <summary>Metres around a cell that count as its surroundings.</summary>
        private const float SurroundReach = 60f;

        /// <summary>Metres below its surroundings at which a cell holds full mist.</summary>
        private const float FullHollow = 5f;

        /// <summary>Metres above the water at which a bank no longer holds mist.</summary>
        private const float BankHeight = 2.5f;

        /// <summary>Metres the mist rests on, per cell; the water's surface where the ground is under it.</summary>
        public NativeArray<float> Ground;

        /// <summary>From 0 to 1 in each cell.</summary>
        public NativeArray<float> Pooling;

        public MistField(int cells)
        {
            Ground = new NativeArray<float>(cells, Allocator.Persistent);
            Pooling = new NativeArray<float>(cells, Allocator.Persistent);
        }

        /// <summary>Water counts only when the terrain dips under <paramref name="waterLevel"/> somewhere.</summary>
        public void Lay(in CellGrid grid, in TerrainHeight terrain, float waterLevel)
        {
            var heights = new NativeArray<float>(Ground.Length, Allocator.Temp);
            var lowest = float.MaxValue;
            for (var i = 0; i < heights.Length; i++)
            {
                heights[i] = terrain.Height(grid.Center(grid.Cell(i)));
                lowest = math.min(lowest, heights[i]);
            }

            var reach = math.max((int)math.round(SurroundReach / grid.CellSize), 1);
            var eastWest = Mean(heights, reach, true);
            var surroundings = Mean(eastWest, reach, false);
            eastWest.Dispose();
            var hasWater = lowest < waterLevel;
            for (var i = 0; i < heights.Length; i++)
            {
                var hollow = math.saturate((surroundings[i] - heights[i]) / FullHollow);
                var bank = hasWater ? math.saturate(1f - (heights[i] - waterLevel) / BankHeight) : 0f;
                Pooling[i] = math.max(hollow, bank);
                Ground[i] = hasWater ? math.max(heights[i], waterLevel) : heights[i];
            }

            heights.Dispose();
            surroundings.Dispose();
        }

        public void Dispose()
        {
            Ground.Dispose();
            Pooling.Dispose();
        }

        // A box mean along x or z; the edge cell stands in past the edge.
        private static NativeArray<float> Mean(NativeArray<float> source, int reach, bool alongX)
        {
            var mean = new NativeArray<float>(source.Length, Allocator.Temp);
            var cells = BattleHaze.Cells;
            var line = alongX ? 1 : cells;
            for (var i = 0; i < source.Length; i++)
            {
                var along = alongX ? i % cells : i / cells;
                var sum = 0f;
                for (var step = -reach; step <= reach; step++)
                {
                    sum += source[i + (math.clamp(along + step, 0, cells - 1) - along) * line];
                }

                mean[i] = sum / (reach * 2 + 1);
            }

            return mean;
        }
    }
}
