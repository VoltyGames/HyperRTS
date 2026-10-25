using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Navigation
{
    /// <summary>
    /// Singleton surface grid over the map: each cell holds <see cref="NavSurface"/> flags, and an agent may enter
    /// cells sharing a flag with its <see cref="NavLayer"/>. Also answers how high each layer stands.
    /// </summary>
    public struct NavGrid : IComponentData
    {
        public NativeArray<byte> Cells;

        /// <summary>
        /// Smallest <see cref="NavAgent.SceneryCrushLimit"/> that drives through each cell's scenery; 0 for none.
        /// </summary>
        public NativeArray<byte> Scenery;

        /// <summary>The movement classes of the match's units, carried over when the grid is rebuilt.</summary>
        public MovementClasses Classes;

        /// <summary>Distance to walls per field key; built only in worlds that move units (see <see cref="NavFieldSystem"/>).</summary>
        public NavField Field;

        /// <summary>Which cells connect, per movement class, for <see cref="IsReachable"/> and path corridors.</summary>
        public NavSectors Sectors;

        public NavChanges Changes;

        /// <summary>Surface height of <see cref="NavSurface.Deck"/> cells; other entries are unused.</summary>
        public NativeArray<float> DeckHeights;

        public TerrainHeight Terrain;

        /// <summary>Height ships ride at; terrain below it is water.</summary>
        public float WaterLevel;

        public int2 Size;
        public float2 Min;
        public float CellSize;

        /// <summary>
        /// Bumped whenever cells close or open, so paths near the <see cref="Changes"/> revalidate; scenery changes
        /// alone don't bump it.
        /// </summary>
        public int Version;

        public readonly bool IsCreated => Cells.IsCreated;

        /// <summary>A blank grid with no movement classes yet; the caller fills the cells and disposes it.</summary>
        public static NavGrid Create(int2 size, float2 min, float cellSize, in TerrainHeight terrain, float waterLevel)
        {
            var count = size.x * size.y;
            var field = NavField.Create(size, min, cellSize, 0, Allocator.Persistent);
            return new NavGrid
            {
                Cells = new NativeArray<byte>(count, Allocator.Persistent),
                Scenery = new NativeArray<byte>(count, Allocator.Persistent),
                Classes = MovementClasses.Create(Allocator.Persistent),
                Field = field,
                Sectors = NavSectors.Create(0, field.Sectors, Allocator.Persistent),
                DeckHeights = new NativeArray<float>(count, Allocator.Persistent),
                Changes = NavChanges.Create(count, Allocator.Persistent),
                Terrain = terrain,
                WaterLevel = waterLevel,
                Size = size,
                Min = min,
                CellSize = cellSize,
            };
        }

        public void Dispose()
        {
            Cells.Dispose();
            Scenery.Dispose();
            Classes.Dispose();
            Field.Dispose();
            Sectors.Dispose();
            DeckHeights.Dispose();
            Changes.Dispose();
        }

        public readonly int2 WorldToCell(float3 position) => WorldToCell(position.xz);

        public readonly int2 WorldToCell(float2 position) => (int2)math.floor((position - Min) / CellSize);

        public readonly float3 CellCenter(int2 cell) =>
            new(Min.x + (cell.x + 0.5f) * CellSize, 0f, Min.y + (cell.y + 0.5f) * CellSize);

        /// <summary>The distance from segment ab to the cell's square.</summary>
        public readonly float CellGap(int2 cell, float2 a, float2 b)
        {
            var low = Min + (float2)cell * CellSize;
            return NavCapsule.SegmentRectDistance(a, b, low, low + CellSize);
        }

        public readonly bool InBounds(int2 cell) => math.all(cell >= 0 & cell < Size);

        public readonly int Index(int2 cell) => cell.y * Size.x + cell.x;

        public readonly int2 Cell(int index) => new(index % Size.x, index / Size.x);

        public readonly NavSurface Surface(int2 cell) =>
            InBounds(cell) ? (NavSurface)Cells[Index(cell)] : NavSurface.Blocked;

        public readonly bool IsWalkable(int2 cell, NavLayer layer = NavLayer.Ground) =>
            IsOpen(cell, NavLayers.Surfaces(layer));

        public readonly bool IsWalkable(float3 position, NavLayer layer = NavLayer.Ground) =>
            IsWalkable(WorldToCell(position), layer);

        /// <summary>The visible top: a deck, else the water surface over submerged ground, else the ground.</summary>
        public readonly float SurfaceHeight(float3 position)
        {
            var cell = math.clamp(WorldToCell(position), 0, Size - 1);
            var surface = (NavSurface)Cells[Index(cell)];
            if ((surface & NavSurface.Deck) != 0)
            {
                return DeckHeights[Index(cell)];
            }

            var ground = Terrain.Height(position.xz);
            return (surface & NavSurface.Water) != 0 ? math.max(ground, WaterLevel) : ground;
        }

        /// <summary>Where an agent of a layer stands: ships float at the water level, even under a deck.</summary>
        public readonly float HeightFor(float3 position, NavLayer layer) =>
            layer == NavLayer.Naval ? WaterLevel : SurfaceHeight(position);

        /// <summary>First and last cell under an XZ box (may lie outside the grid).</summary>
        public readonly void GetArea(float3 center, float2 size, out int2 min, out int2 max)
        {
            var half = new float3(size.x * 0.5f, 0f, size.y * 0.5f);
            min = WorldToCell(center - half);
            max = WorldToCell(center + half - 0.001f);
        }

        /// <summary><see cref="GetArea"/> clamped to the grid; empty (min above max) when the box is off the map.</summary>
        public readonly void GetClampedArea(float3 center, float2 size, out int2 min, out int2 max)
        {
            GetArea(center, size, out min, out max);
            min = math.max(min, 0);
            max = math.min(max, Size - 1);
        }

        /// <summary>Clamped cell range of a turned box's world-axis bounds; test each cell with <see cref="InBox"/>.</summary>
        public readonly void GetBoxArea(in NavBox box, out int2 min, out int2 max) =>
            GetClampedArea(new float3(box.Center.x, 0f, box.Center.y), box.Extents * 2f, out min, out max);

        /// <summary>Whether a cell in <see cref="GetBoxArea"/>'s range is covered: any overlap with the turned box counts.</summary>
        public readonly bool InBox(in NavBox box, int2 cell) =>
            box.IsAxisAligned || box.Contains(CellCenter(cell).xz, CellSize * 0.5f);

        /// <summary>
        /// Whether an agent could path between two points: their cells share a region, scenery aside. A start in
        /// closed cells counts from the open ground it would step out to. Aircraft reach everywhere.
        /// </summary>
        public readonly bool IsReachable(in NavAgent agent, float3 from, float3 to)
        {
            if (agent.Layer == NavLayer.Air)
            {
                return true;
            }

            var access = agent.Access(this);
            if (!access.TryFindNearest(WorldToCell(from), GridPathfinder.NearestSearchRadius, out var start))
            {
                return false;
            }

            return access.Region(start) == access.Region(WorldToCell(to));
        }

        /// <summary>The surface the ground alone gives a cell (<see cref="NavGround.Classify"/>).</summary>
        public readonly NavSurface GroundSurface(int index, bool flood, float maxGradient) =>
            NavGround.Classify(Terrain, CellCenter(Cell(index)).xz, CellSize, WaterLevel, flood, maxGradient);

        /// <summary>
        /// Lays a nav area over the cells under its box, as obstacles do with <see cref="NavAreaKind.Blocked"/>. Decks
        /// go after the other areas, so they span water, and record their height.
        /// </summary>
        public void StampArea(in NavBox box, NavAreaKind kind, float height)
        {
            GetBoxArea(box, out var min, out var max);
            for (var y = min.y; y <= max.y; y++)
            {
                for (var x = min.x; x <= max.x; x++)
                {
                    var cell = new int2(x, y);
                    if (!InBox(box, cell))
                    {
                        continue;
                    }

                    var index = Index(cell);
                    Cells[index] = (byte)NavGround.Stamp(kind, (NavSurface)Cells[index]);
                    if (kind == NavAreaKind.Deck)
                    {
                        DeckHeights[index] = height;
                    }
                }
            }
        }

        /// <summary>Records the box this version changed (<see cref="Changes"/>) and marks the field sectors round each changed cell.</summary>
        public void RecordChanges()
        {
            Changes.Record(this);
            // Cell by cell, so wrecks far apart in one restamp don't rebuild every sector of the box between them.
            Field.MarkChanges(Cells, Changes.Previous);
        }

        /// <summary>
        /// Sizes the field and sector graph to the registered classes after a registration; returns the first field
        /// slot that is new, so needs a full build. A new class relabels every sector.
        /// </summary>
        public int FitAccess()
        {
            var built = Field.Keys;
            if (Classes.Keys.Length > built)
            {
                Field.Grow(Classes.Keys.Length, Allocator.Persistent);
            }

            if (Sectors.Classes != Classes.Classes.Length)
            {
                Sectors.Dispose();
                Sectors = NavSectors.Create(Classes.Classes.Length, Field.Sectors, Allocator.Persistent);
            }

            return built;
        }

        /// <summary>Builds the whole field and sector graph for the registered classes at once, as editor tools need.</summary>
        public void BuildAccess()
        {
            FitAccess();
            var transform = new DistanceTransform(Field.WindowSide, Allocator.Temp);
            for (var slot = 0; slot < Classes.Keys.Length; slot++)
            {
                for (var sector = 0; sector < Field.DirtySectors.Length; sector++)
                {
                    Field.Rebuild(Cells, Scenery, Classes.Keys[slot], slot, sector, ref transform);
                }
            }

            transform.Dispose();
            Sectors.Build(Field, Classes);
            for (var sector = 0; sector < Field.DirtySectors.Length; sector++)
            {
                Field.DirtySectors[sector] = false;
            }
        }

        internal readonly bool IsOpen(int2 cell, NavSurface surfaces) =>
            InBounds(cell) && ((NavSurface)Cells[Index(cell)] & surfaces) != 0;
    }
}
