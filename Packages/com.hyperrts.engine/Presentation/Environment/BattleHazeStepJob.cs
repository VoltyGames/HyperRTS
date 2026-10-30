using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace HyperRTS.Presentation.Environment
{
    /// <summary>One <see cref="BattleHaze"/> step: drift downwind, spread to the neighbours, thin, then write the texels.</summary>
    [BurstCompile]
    public struct BattleHazeStepJob : IJob
    {
        private const int Cells = BattleHaze.Cells;

        public NativeArray<float> Amounts;
        public NativeArray<float> Drifted;

        [WriteOnly] public NativeArray<half> Texels;

        /// <summary>Cells the haze moves this step.</summary>
        public float2 Shift;

        /// <summary>Share of each cell that evens out with its neighbours.</summary>
        public float Share;

        /// <summary>Share of the haze left after thinning.</summary>
        public float Keep;

        public int2 Roll;

        public void Execute()
        {
            Drift();
            SpreadAndThin();
            WriteTexels();
        }

        // Each cell takes what lay upwind of it; clean air blows in past the map's edge.
        private void Drift()
        {
            for (var z = 0; z < Cells; z++)
            {
                for (var x = 0; x < Cells; x++)
                {
                    var source = new float2(x, z) - Shift;
                    var low = (int2)math.floor(source);
                    var t = source - low;
                    var bottom = math.lerp(At(low), At(low + new int2(1, 0)), t.x);
                    var top = math.lerp(At(low + new int2(0, 1)), At(low + 1), t.x);
                    Drifted[x + z * Cells] = math.lerp(bottom, top, t.y);
                }
            }
        }

        // Neighbours past the edge count as the cell itself, so spreading never loses haze.
        private void SpreadAndThin()
        {
            for (var z = 0; z < Cells; z++)
            {
                for (var x = 0; x < Cells; x++)
                {
                    var here = Drifted[x + z * Cells];
                    var around = Clamped(x - 1, z) + Clamped(x + 1, z) + Clamped(x, z - 1) + Clamped(x, z + 1);
                    var spread = here + Share * (around * 0.25f - here);
                    Amounts[x + z * Cells] = math.min(spread * Keep, BattleHaze.Most);
                }
            }
        }

        private void WriteTexels()
        {
            for (var z = 0; z < Cells; z++)
            {
                var cellZ = Wrap(z - Roll.y);
                for (var y = 0; y < BattleHaze.Layers; y++)
                {
                    var row = (z * BattleHaze.Layers + y) * Cells;
                    for (var x = 0; x < Cells; x++)
                    {
                        Texels[row + x] = (half)(1f + Amounts[Wrap(x - Roll.x) + cellZ * Cells]);
                    }
                }
            }
        }

        private readonly float At(int2 cell)
        {
            var onGrid = math.all(cell >= 0) && math.all(cell < Cells);
            return onGrid ? Amounts[cell.x + cell.y * Cells] : 0f;
        }

        private readonly float Clamped(int x, int z) =>
            Drifted[math.clamp(x, 0, Cells - 1) + math.clamp(z, 0, Cells - 1) * Cells];

        private static int Wrap(int cell) => (cell % Cells + Cells) % Cells;
    }
}
