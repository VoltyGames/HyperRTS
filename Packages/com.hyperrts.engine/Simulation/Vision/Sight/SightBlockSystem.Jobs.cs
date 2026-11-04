using HyperRTS.Simulation.Navigation.Heights;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.Vision.Sight
{
    public partial struct SightBlockSystem
    {
        /// <summary>Keeps the last stamp to compare with before blocks stamp again.</summary>
        [BurstCompile]
        private struct SavePreviousJob : IJob
        {
            [ReadOnly] public SightBlockers Blockers;
            public NativeArray<SightCell> Previous;

            public void Execute() => Previous.CopyFrom(Blockers.Cells);
        }

        [BurstCompile]
        private struct ClearBlocksJob : IJobParallelFor
        {
            public NativeArray<SightCell> Cells;

            public void Execute(int index)
            {
                var cell = Cells[index];
                cell.ClearBlocks();
                Cells[index] = cell;
            }
        }

        /// <summary>Marks the tiles whose sight the restamp changed, so only viewers near them draw again.</summary>
        [BurstCompile]
        private struct DiffJob : IJob
        {
            public SightBlockers Blockers;
            [ReadOnly] public NativeArray<SightCell> Previous;

            public void Execute()
            {
                for (var i = 0; i < Previous.Length; i++)
                {
                    if (Blockers.Cells[i].BlocksUnlike(Previous[i]))
                    {
                        Blockers.MarkDirty(Blockers.Grid.Cell(i));
                    }
                }
            }
        }

        [BurstCompile]
        private struct GroundJob : IJobParallelFor
        {
            [NativeDisableParallelForRestriction] public SightBlockers Blockers;
            public TerrainHeight Terrain;

            public void Execute(int index)
            {
                var grid = Blockers.Grid;
                var cell = Blockers.Cells[index];
                cell.Ground = Terrain.Height(grid.Center(grid.Cell(index)));
                Blockers.Cells[index] = cell;
            }
        }

        /// <summary>Single-threaded: overlapping blocks raise the same cells.</summary>
        [BurstCompile]
        private partial struct StampJob : IJobEntity
        {
            public SightBlockers Blockers;
            public TerrainHeight Terrain;

            private void Execute(Entity entity, in SightBlock block, in LocalTransform transform)
            {
                using var marker = StampMarker.Auto();
                Blockers.Stamp(entity, block, transform, Terrain);
            }
        }

        /// <summary>
        /// Each fog cell takes the mean density of the crop cells whose centres fall in it, and the tallest crop's
        /// top, so a field's ragged edge thins its cover.
        /// </summary>
        [BurstCompile]
        private struct CropJob : IJob
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
}
