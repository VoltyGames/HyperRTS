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

        /// <summary>The block whose solid top each cell rises to, so a blocked shot knows what it hit.</summary>
        public NativeArray<Entity> Owner;

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

        public GridFrame Frame;

        /// <summary>Bumped whenever the grid is built anew, so every viewer's sight is drawn again.</summary>
        public int Generation;

        public readonly bool IsCreated => Solid.IsCreated;

        /// <summary>An empty grid laid out as the map's fog grid.</summary>
        public static SightBlockers Create(in MapSettings map, Allocator allocator)
        {
            var frame = map.FogFrame();
            var count = frame.Count;
            var tiles = (frame.Size + TileCells - 1) / TileCells;
            return new SightBlockers
            {
                DirtyTiles = new NativeArray<byte>(tiles.x * tiles.y, allocator),
                Tiles = tiles,
                Ground = new NativeArray<float>(count, allocator),
                Owner = new NativeArray<Entity>(count, allocator),
                Solid = new NativeArray<byte>(count, allocator),
                Porous = new NativeArray<byte>(count, allocator),
                PorousTop = new NativeArray<byte>(count, allocator),
                Crop = new NativeArray<byte>(count, allocator),
                CropTop = new NativeArray<byte>(count, allocator),
                Conceals = new NativeArray<byte>(count, allocator),
                Frame = frame,
            };
        }

        public readonly void Dispose()
        {
            Ground.Dispose();
            Owner.Dispose();
            Solid.Dispose();
            Porous.Dispose();
            PorousTop.Dispose();
            Crop.Dispose();
            CropTop.Dispose();
            Conceals.Dispose();
            DirtyTiles.Dispose();
        }

        /// <summary>
        /// Whether the straight line from <paramref name="from"/> to <paramref name="to"/> runs into the ground or a
        /// solid block, and where; the blocks of <paramref name="shooter"/> and <paramref name="target"/> don't count.
        /// </summary>
        public readonly bool TryFindObstruction(float3 from, float3 to, Entity shooter, Entity target,
            out float3 contact, out Entity obstacle)
        {
            var ray = new GridRay(Frame, from.xz, to.xz);
            while (ray.MoveNext())
            {
                if (!Frame.Contains(ray.Cell))
                {
                    continue;
                }

                var index = Frame.Index(ray.Cell);
                obstacle = Owner[index];
                var own = obstacle != Entity.Null && (obstacle == shooter || obstacle == target);
                // Shooter and target stand on their own ground, which their cell's one height only roughs out.
                var end = ray.Entry == 0f || ray.Exit >= 1f;
                if (end && Solid[index] == 0)
                {
                    continue;
                }

                if (!own && TryFindContact(math.lerp(from, to, ray.Entry), math.lerp(from, to, ray.Exit),
                        Ground[index] + SolidHeight(index), out contact))
                {
                    return true;
                }
            }

            contact = to;
            obstacle = Entity.Null;
            return false;
        }

        /// <summary>Share of sight the porous cover along the line takes, such as smoke between a missile and its target.</summary>
        public readonly float Haze(float3 from, float3 to)
        {
            var length = math.distance(from.xz, to.xz);
            var haze = 0f;
            var ray = new GridRay(Frame, from.xz, to.xz);
            while (ray.MoveNext())
            {
                if (Frame.Contains(ray.Cell))
                {
                    var index = Frame.Index(ray.Cell);
                    var middle = math.lerp(from.y, to.y, (ray.Entry + ray.Exit) * 0.5f);
                    haze += Loss(index, middle - Ground[index], (ray.Exit - ray.Entry) * length);
                }
            }

            return haze;
        }

        public void MarkDirty(int2 cell) => DirtyTiles[cell.y / TileCells * Tiles.x + cell.x / TileCells] = 1;

        /// <summary>Whether sight changed anywhere in the cells from <paramref name="min"/> to <paramref name="max"/>.</summary>
        public readonly bool IsDirty(int2 min, int2 max)
        {
            var low = math.max(min, 0) / TileCells;
            var high = math.min(max, Frame.Size - 1) / TileCells;
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
            var cell = Frame.Cell(position);
            return Frame.Contains(cell) && IsConcealing(Frame.Index(cell));
        }

        /// <summary>Raises every cell the block touches to its tops and densities, rounded up to a step.</summary>
        public void Stamp(Entity entity, in SightBlock block, in LocalTransform transform, in TerrainHeight terrain)
        {
            var box = block.Box(transform);
            foreach (var cell in CellsUnder(box))
            {
                var center = Frame.Center(cell);
                if (!Touches(box, center))
                {
                    continue;
                }

                var footing = transform.Position.y - terrain.Height(center);
                var index = Frame.Index(cell);
                var solid = Solid[index];
                Raise(Solid, index, (footing + block.Height) / Step, block.Height > 0f);
                if (Solid[index] > solid)
                {
                    Owner[index] = entity;
                }

                Raise(PorousTop, index, (footing + block.PorousTop) / Step, block.Density > 0f);
                Raise(Porous, index, block.Density / DensityStep, block.Density > 0f);
                Raise(Conceals, index, 1f, block.Conceals);
            }
        }

        /// <summary>Clears the crop cover on every cell the box touches.</summary>
        public void Flatten(in NavBox box)
        {
            foreach (var cell in CellsUnder(box))
            {
                var index = Frame.Index(cell);
                if (Crop[index] != 0 && Touches(box, Frame.Center(cell)))
                {
                    Crop[index] = 0;
                    MarkDirty(cell);
                }
            }
        }

        private readonly CellRange CellsUnder(in NavBox box) =>
            Frame.Cells(Frame.Cell(box.Center - box.Extents), Frame.Cell(box.Center + box.Extents));

        // The line is lowest at one end of its crossing, so it meets the top there or where it sinks below it.
        private static bool TryFindContact(float3 entry, float3 exit, float top, out float3 contact)
        {
            if (entry.y < top)
            {
                contact = entry;
                return true;
            }

            if (exit.y >= top)
            {
                contact = exit;
                return false;
            }

            contact = math.lerp(entry, exit, (entry.y - top) / (entry.y - exit.y));
            return true;
        }

        // Thin walls and fences block every cell they cross, not only the ones whose centre they cover.
        private readonly bool Touches(in NavBox box, float2 center) =>
            box.Overlaps(new NavBox(new float3(center.x, 0f, center.y), quaternion.identity,
                new float2(Frame.CellSize)));

        private static void Raise(NativeArray<byte> cells, int index, float steps, bool applies)
        {
            if (applies)
            {
                cells[index] = (byte)math.max(cells[index], (int)math.clamp(math.ceil(steps), 0f, byte.MaxValue));
            }
        }
    }
}
