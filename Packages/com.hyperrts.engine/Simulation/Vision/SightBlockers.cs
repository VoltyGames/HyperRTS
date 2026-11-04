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

        /// <summary>Ground and blockers of each cell.</summary>
        public NativeArray<SightCell> Cells;

        /// <summary>The block whose solid top each cell rises to, so a blocked shot knows what it hit.</summary>
        public NativeArray<Entity> Owner;

        /// <summary>Squares of <see cref="TileCells"/> cells whose sight changed since the fog last drew round them.</summary>
        public NativeArray<byte> DirtyTiles;

        public int2 Tiles;

        public CellGrid Grid;

        /// <summary>Bumped whenever the grid is built anew, so every viewer's sight is drawn again.</summary>
        public int Generation;

        public readonly bool IsCreated => Cells.IsCreated;

        /// <summary>An empty grid laid out as the map's fog grid.</summary>
        /// <summary>The singleton's query, for a system to create in OnCreate and read in OnUpdate.</summary>
        public static EntityQuery Query(ref SystemState state) =>
            new EntityQueryBuilder(Allocator.Temp).WithAll<SightBlockers>().Build(ref state);

        public static SightBlockers Create(in MapSettings map, Allocator allocator)
        {
            var grid = map.FogGrid();
            var count = grid.Count;
            var tiles = (grid.Size + TileCells - 1) / TileCells;
            return new SightBlockers
            {
                DirtyTiles = new NativeArray<byte>(tiles.x * tiles.y, allocator),
                Tiles = tiles,
                Cells = new NativeArray<SightCell>(count, allocator),
                Owner = new NativeArray<Entity>(count, allocator),
                Grid = grid,
            };
        }

        public readonly void Dispose()
        {
            Cells.Dispose();
            Owner.Dispose();
            DirtyTiles.Dispose();
        }

        /// <summary>
        /// Whether the straight line from <paramref name="from"/> to <paramref name="to"/> runs into the ground or a
        /// solid block, and where; the blocks of <paramref name="shooter"/> and <paramref name="target"/> don't count.
        /// </summary>
        public readonly bool TryFindObstruction(float3 from, float3 to, Entity shooter, Entity target,
            out float3 contact, out Entity obstacle)
        {
            var ray = new GridRay(Grid, from.xz, to.xz);
            while (ray.MoveNext())
            {
                if (!Grid.Contains(ray.Cell))
                {
                    continue;
                }

                var index = Grid.Index(ray.Cell);
                obstacle = Owner[index];
                var own = obstacle != Entity.Null && (obstacle == shooter || obstacle == target);
                // Shooter and target stand on their own ground, which their cell's one height only roughs out.
                var end = ray.Entry == 0f || ray.Exit >= 1f;
                var cell = Cells[index];
                if (end && cell.Solid == 0)
                {
                    continue;
                }

                if (!own && TryFindContact(math.lerp(from, to, ray.Entry), math.lerp(from, to, ray.Exit),
                        cell.Ground + cell.SolidHeight, out contact))
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
            var ray = new GridRay(Grid, from.xz, to.xz);
            while (ray.MoveNext())
            {
                if (Grid.Contains(ray.Cell))
                {
                    var cell = Cells[Grid.Index(ray.Cell)];
                    var middle = math.lerp(from.y, to.y, (ray.Entry + ray.Exit) * 0.5f);
                    haze += cell.Loss(middle - cell.Ground, (ray.Exit - ray.Entry) * length);
                }
            }

            return haze;
        }

        public void MarkDirty(int2 cell) => DirtyTiles[cell.y / TileCells * Tiles.x + cell.x / TileCells] = 1;

        /// <summary>Whether sight changed anywhere in the cells from <paramref name="min"/> to <paramref name="max"/>.</summary>
        public readonly bool IsDirty(int2 min, int2 max)
        {
            var low = math.max(min, 0) / TileCells;
            var high = math.min(max, Grid.Size - 1) / TileCells;
            foreach (var tile in new CellRange(low, high))
            {
                if (DirtyTiles[tile.y * Tiles.x + tile.x] != 0)
                {
                    return true;
                }
            }

            return false;
        }

        public readonly bool IsConcealing(float3 position)
        {
            var cell = Grid.Cell(position);
            return Grid.Contains(cell) && Cells[Grid.Index(cell)].IsConcealing;
        }

        /// <summary>Raises every cell the block touches to its tops and densities, rounded up to a step.</summary>
        public void Stamp(Entity entity, in SightBlock block, in LocalTransform transform, in TerrainHeight terrain)
        {
            var box = block.Box(transform);
            foreach (var cell in CellsUnder(box))
            {
                var center = Grid.Center(cell);
                if (!Touches(box, center))
                {
                    continue;
                }

                var footing = transform.Position.y - terrain.Height(center);
                var index = Grid.Index(cell);
                var sight = Cells[index];
                var solid = sight.Solid;
                Raise(ref sight.Solid, (footing + block.Height) / Step, block.Height > 0f);
                if (sight.Solid > solid)
                {
                    Owner[index] = entity;
                }

                Raise(ref sight.PorousTop, (footing + block.PorousTop) / Step, block.Density > 0f);
                Raise(ref sight.Porous, block.Density / DensityStep, block.Density > 0f);
                Raise(ref sight.Conceals, 1f, block.Conceals);
                Cells[index] = sight;
            }
        }

        /// <summary>Clears the crop cover on every cell the box touches.</summary>
        public void Flatten(in NavBox box)
        {
            foreach (var cell in CellsUnder(box))
            {
                var index = Grid.Index(cell);
                var sight = Cells[index];
                if (sight.Crop != 0 && Touches(box, Grid.Center(cell)))
                {
                    sight.Crop = 0;
                    Cells[index] = sight;
                    MarkDirty(cell);
                }
            }
        }

        private readonly CellRange CellsUnder(in NavBox box) =>
            Grid.Cells(Grid.Cell(box.Center - box.Extents), Grid.Cell(box.Center + box.Extents));

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
                new float2(Grid.CellSize)));

        private static void Raise(ref byte layer, float steps, bool applies)
        {
            if (applies)
            {
                layer = (byte)math.max(layer, (int)math.clamp(math.ceil(steps), 0f, byte.MaxValue));
            }
        }
    }
}
