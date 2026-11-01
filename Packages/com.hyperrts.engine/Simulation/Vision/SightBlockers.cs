using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Navigation.Areas;
using HyperRTS.Simulation.Navigation.Heights;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.Vision
{
    /// <summary>
    /// Singleton on the <see cref="FogOfWar"/> grid: what blocks sight on each cell. Solid <see cref="SightBlock"/>s
    /// raise its ground; porous ones and crops take a share of sight per metre crossed below their top; some conceal.
    /// Heights are above the cell's ground in <see cref="Step"/> metre steps, densities in <see cref="DensityStep"/>s.
    /// </summary>
    public struct SightBlockers : IComponentData
    {
        public const float Step = 0.25f;
        public const float DensityStep = 1f / 250f;

        /// <summary>Cells across a square of <see cref="DirtyTiles"/>.</summary>
        public const int TileCells = 8;

        /// <summary>Terrain height at each cell's centre, so sight lines needn't sample the heightfield.</summary>
        public NativeArray<float> Ground;

        public NativeArray<byte> Solid;
        public NativeArray<byte> Porous;
        public NativeArray<byte> PorousTop;

        /// <summary>Crop cover from the map's <see cref="CropCover"/>, cleared where vehicles flatten it.</summary>
        public NativeArray<byte> Crop;

        public NativeArray<byte> CropTop;

        /// <summary>Cells a block conceals on, such as ruins and trenches; woods and crops conceal too.</summary>
        public NativeArray<byte> Conceals;

        /// <summary>Squares of <see cref="TileCells"/> cells whose sight changed since the fog last drew round them.</summary>
        public NativeArray<byte> DirtyTiles;

        public int2 Tiles;

        public int2 Size;
        public float2 Min;
        public float CellSize;

        /// <summary>Bumped whenever the blocks are restamped.</summary>
        public int Version;

        /// <summary>Bumped whenever the grid is built anew, so every viewer's sight is drawn again.</summary>
        public int Generation;

        public readonly bool IsCreated => Solid.IsCreated;

        /// <summary>An empty grid laid out as the map's fog grid.</summary>
        public static SightBlockers Create(in MapSettings map, Allocator allocator)
        {
            map.FogLayout(out var size, out var cellSize);
            var count = size.x * size.y;
            var tiles = (size + TileCells - 1) / TileCells;
            return new SightBlockers
            {
                DirtyTiles = new NativeArray<byte>(tiles.x * tiles.y, allocator),
                Tiles = tiles,
                Ground = new NativeArray<float>(count, allocator),
                Solid = new NativeArray<byte>(count, allocator),
                Porous = new NativeArray<byte>(count, allocator),
                PorousTop = new NativeArray<byte>(count, allocator),
                Crop = new NativeArray<byte>(count, allocator),
                CropTop = new NativeArray<byte>(count, allocator),
                Conceals = new NativeArray<byte>(count, allocator),
                Size = size,
                Min = map.Min,
                CellSize = cellSize,
            };
        }

        public readonly void Dispose()
        {
            Ground.Dispose();
            Solid.Dispose();
            Porous.Dispose();
            PorousTop.Dispose();
            Crop.Dispose();
            CropTop.Dispose();
            Conceals.Dispose();
            DirtyTiles.Dispose();
        }

        public void MarkDirty(int2 cell) => DirtyTiles[cell.y / TileCells * Tiles.x + cell.x / TileCells] = 1;

        /// <summary>Whether sight changed anywhere in the cells from <paramref name="min"/> to <paramref name="max"/>.</summary>
        public readonly bool IsDirty(int2 min, int2 max)
        {
            var low = math.max(min, 0) / TileCells;
            var high = math.min(max, Size - 1) / TileCells;
            for (var y = low.y; y <= high.y; y++)
            {
                for (var x = low.x; x <= high.x; x++)
                {
                    if (DirtyTiles[y * Tiles.x + x] != 0)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        public readonly float SolidHeight(int index) => Solid[index] * Step;

        /// <summary>Share of sight lost crossing <paramref name="length"/> metres of the cell at <paramref name="height"/> above its ground.</summary>
        public readonly float Loss(int index, float height, float length)
        {
            var porous = height < PorousTop[index] * Step ? Porous[index] : 0;
            var crop = height < CropTop[index] * Step ? Crop[index] : 0;
            return (porous + crop) * DensityStep * length;
        }

        public readonly bool IsConcealing(int index) => (Conceals[index] | Porous[index] | Crop[index]) != 0;

        public readonly bool IsConcealing(float3 position)
        {
            var cell = (int2)math.floor((position.xz - Min) / CellSize);
            return InBounds(cell) && IsConcealing(Index(cell));
        }

        public readonly bool InBounds(int2 cell) => math.all(cell >= 0 & cell < Size);

        public readonly int Index(int2 cell) => cell.y * Size.x + cell.x;

        /// <summary>Raises every cell the block touches to its tops and densities, rounded up to a step.</summary>
        public void Stamp(in SightBlock block, in LocalTransform transform, in TerrainHeight terrain)
        {
            var box = block.Box(transform);
            GetCells(box, out var min, out var max);
            for (var y = min.y; y <= max.y; y++)
            {
                for (var x = min.x; x <= max.x; x++)
                {
                    var center = Min + (new float2(x, y) + 0.5f) * CellSize;
                    if (!Touches(box, center))
                    {
                        continue;
                    }

                    var footing = transform.Position.y - terrain.Height(center);
                    var index = Index(new int2(x, y));
                    Raise(Solid, index, (footing + block.Height) / Step, block.Height > 0f);
                    Raise(PorousTop, index, (footing + block.PorousTop) / Step, block.Density > 0f);
                    Raise(Porous, index, block.Density / DensityStep, block.Density > 0f);
                    Raise(Conceals, index, 1f, block.Conceals);
                }
            }
        }

        /// <summary>Clears the crop cover on every cell the box touches.</summary>
        public void Flatten(in NavBox box)
        {
            GetCells(box, out var min, out var max);
            for (var y = min.y; y <= max.y; y++)
            {
                for (var x = min.x; x <= max.x; x++)
                {
                    var index = Index(new int2(x, y));
                    if (Crop[index] != 0 && Touches(box, Min + (new float2(x, y) + 0.5f) * CellSize))
                    {
                        Crop[index] = 0;
                        MarkDirty(new int2(x, y));
                    }
                }
            }
        }

        private readonly void GetCells(in NavBox box, out int2 min, out int2 max)
        {
            var extents = box.Extents;
            min = math.max((int2)math.floor((box.Center - extents - Min) / CellSize), 0);
            max = math.min((int2)math.floor((box.Center + extents - Min) / CellSize), Size - 1);
        }

        // Thin walls and fences block every cell they cross, not only the ones whose centre they cover.
        private readonly bool Touches(in NavBox box, float2 center) =>
            box.Overlaps(new NavBox(new float3(center.x, 0f, center.y), quaternion.identity, new float2(CellSize)));

        private static void Raise(NativeArray<byte> cells, int index, float steps, bool applies)
        {
            if (applies)
            {
                cells[index] = (byte)math.max(cells[index], (int)math.clamp(math.ceil(steps), 0f, byte.MaxValue));
            }
        }
    }
}
