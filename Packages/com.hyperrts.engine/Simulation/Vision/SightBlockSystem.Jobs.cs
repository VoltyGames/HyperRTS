using HyperRTS.Simulation.Navigation.Heights;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.Vision
{
    public partial struct SightBlockSystem
    {
        /// <summary>Keeps the last stamp to compare with before blocks stamp again.</summary>
        [BurstCompile]
        private struct KeepJob : IJob
        {
            [ReadOnly] public SightBlockers Blockers;
            public NativeArray<byte> Previous;

            public void Execute()
            {
                var count = Blockers.Solid.Length;
                NativeArray<byte>.Copy(Blockers.Solid, 0, Previous, 0, count);
                NativeArray<byte>.Copy(Blockers.Porous, 0, Previous, count, count);
                NativeArray<byte>.Copy(Blockers.PorousTop, 0, Previous, count * 2, count);
            }
        }

        /// <summary>Marks the tiles whose sight the restamp changed, so only viewers near them draw again.</summary>
        [BurstCompile]
        private struct DiffJob : IJob
        {
            public SightBlockers Blockers;
            [ReadOnly] public NativeArray<byte> Previous;

            public void Execute()
            {
                var count = Blockers.Solid.Length;
                for (var i = 0; i < count; i++)
                {
                    var solid = Blockers.Solid[i] != Previous[i];
                    var porous = Blockers.Porous[i] != Previous[count + i] || Blockers.PorousTop[i] != Previous[count * 2 + i];
                    if (solid || porous)
                    {
                        Blockers.MarkDirty(Blockers.Frame.Cell(i));
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
                var frame = Blockers.Frame;
                Blockers.Ground[index] = Terrain.Height(frame.Center(frame.Cell(index)));
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
                var fog = Blockers.Frame;
                var sums = new NativeArray<int>(Blockers.Crop.Length, Allocator.Temp);
                foreach (var crop in crops.Frame.Cells(int2.zero, crops.Frame.Size - 1))
                {
                    var index = crops.Frame.Index(crop);
                    var cell = fog.Cell(crops.Frame.Center(crop));
                    if (crops.Density[index] == 0 || !fog.Contains(cell))
                    {
                        continue;
                    }

                    var target = fog.Index(cell);
                    sums[target] += crops.Density[index];
                    Blockers.CropTop[target] = (byte)math.max(Blockers.CropTop[target], (int)crops.Top[index]);
                }

                var perCell = math.max(1f, math.square(fog.CellSize / crops.Frame.CellSize));
                for (var i = 0; i < sums.Length; i++)
                {
                    Blockers.Crop[i] = (byte)math.min(byte.MaxValue, (int)math.round(sums[i] / perCell));
                }
            }
        }
    }
}
