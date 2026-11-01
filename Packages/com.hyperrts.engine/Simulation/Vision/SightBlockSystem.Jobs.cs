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
        /// <summary>Keeps the last stamp to compare with, then clears what blocks stamp; crop cover stays, flattened lanes and all.</summary>
        [BurstCompile]
        private struct ClearJob : IJob
        {
            public SightBlockers Blockers;
            public NativeArray<byte> Previous;

            public void Execute()
            {
                var count = Blockers.Solid.Length;
                NativeArray<byte>.Copy(Blockers.Solid, 0, Previous, 0, count);
                NativeArray<byte>.Copy(Blockers.Porous, 0, Previous, count, count);
                NativeArray<byte>.Copy(Blockers.PorousTop, 0, Previous, count * 2, count);
                Clear(Blockers.Solid);
                for (var i = 0; i < Blockers.Owner.Length; i++)
                {
                    Blockers.Owner[i] = Entity.Null;
                }

                Clear(Blockers.Porous);
                Clear(Blockers.PorousTop);
                Clear(Blockers.Conceals);
            }

            private static void Clear(NativeArray<byte> cells)
            {
                for (var i = 0; i < cells.Length; i++)
                {
                    cells[i] = 0;
                }
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
                        Blockers.MarkDirty(new int2(i % Blockers.Size.x, i / Blockers.Size.x));
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
                var cell = new int2(index % Blockers.Size.x, index / Blockers.Size.x);
                Blockers.Ground[index] = Terrain.Height(Blockers.Min + ((float2)cell + 0.5f) * Blockers.CellSize);
            }
        }

        /// <summary>Single-threaded: overlapping blocks raise the same cells.</summary>
        [BurstCompile]
        private partial struct StampJob : IJobEntity
        {
            public SightBlockers Blockers;
            public TerrainHeight Terrain;

            private void Execute(Entity entity, in SightBlock block, in LocalTransform transform) =>
                Blockers.Stamp(entity, block, transform, Terrain);
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
                var sums = new NativeArray<int>(Blockers.Crop.Length, Allocator.Temp);
                for (var y = 0; y < crops.Size.y; y++)
                {
                    for (var x = 0; x < crops.Size.x; x++)
                    {
                        var index = y * crops.Size.x + x;
                        var center = crops.Min + (new float2(x, y) + 0.5f) * crops.Spacing;
                        var cell = (int2)math.floor((center - Blockers.Min) / Blockers.CellSize);
                        if (crops.Density[index] == 0 || !Blockers.InBounds(cell))
                        {
                            continue;
                        }

                        var target = Blockers.Index(cell);
                        sums[target] += crops.Density[index];
                        Blockers.CropTop[target] = (byte)math.max(Blockers.CropTop[target], (int)crops.Top[index]);
                    }
                }

                var perCell = math.max(1f, math.square(Blockers.CellSize / crops.Spacing));
                for (var i = 0; i < sums.Length; i++)
                {
                    Blockers.Crop[i] = (byte)math.min(byte.MaxValue, (int)math.round(sums[i] / perCell));
                }
            }
        }
    }
}
