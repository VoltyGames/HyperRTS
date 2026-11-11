using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Vision.Sight
{
    /// <summary>
    /// Each fog cell takes the mean density of the crop cells whose centres fall in it, and the tallest crop's
    /// top, so a field's ragged edge thins its cover.
    /// </summary>
    [BurstCompile]
    internal struct SightCropJob : IJob
    {
        public SightBlockers Blockers;
        [ReadOnly] public CropCover Crops;

        public void Execute()
        {
            ref var crops = ref Crops.Blob.Value;
            var fog = Blockers.Grid;
            var cells = Blockers.Cells;
            var sums = new NativeArray<int>(cells.Length, Allocator.Temp);
            foreach (var crop in crops.Grid.Cells(int2.zero, crops.Grid.Size - 1))
            {
                var index = crops.Grid.Index(crop);
                var cell = fog.Cell(crops.Grid.Center(crop));
                if (crops.Density[index] == 0 || !fog.Contains(cell))
                {
                    continue;
                }

                var target = fog.Index(cell);
                sums[target] += crops.Density[index];
                var sight = cells[target];
                sight.CropTop = (byte)math.max(sight.CropTop, (int)crops.Top[index]);
                cells[target] = sight;
            }

            var perCell = math.max(1f, math.square(fog.CellSize / crops.Grid.CellSize));
            for (var i = 0; i < sums.Length; i++)
            {
                var sight = cells[i];
                sight.Crop = (byte)math.min(byte.MaxValue, (int)math.round(sums[i] / perCell));
                cells[i] = sight;
            }
        }
    }
}
