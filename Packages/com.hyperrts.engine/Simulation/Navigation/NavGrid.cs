using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Navigation
{
    /// <summary>
    /// Singleton surface grid over the map: each cell holds <see cref="NavSurface"/> flags, and an agent may enter
    /// cells sharing a flag with its <see cref="NavLayer"/>. Also answers how high each layer stands.
    /// </summary>
    public struct NavGrid : IComponentData
    {
        /// <summary>
        /// Most cells one path search expands, so a hopeless request can't stall a worker thread; past it a unit heads
        /// for the closest cell found. Map tools warn when a cross-map path needs more.
        /// </summary>
        public const int MaxSearchExpansions = 20000;

        /// <summary>Layers with a <see cref="Clearance"/> field: ground, naval and amphibious, by layer value.</summary>
        public const int ClearanceLayers = 3;

        public NativeArray<byte> Cells;

        /// <summary>
        /// Smallest <see cref="NavAgent.SceneryCrushLimit"/> that drives through each cell's scenery; 0 for none.
        /// </summary>
        public NativeArray<byte> Scenery;

        /// <summary>Per layer, then per cell: see <see cref="BuildClearance(int)"/>. Scenery never counts as closed here.</summary>
        public NativeArray<byte> Clearance;

        /// <summary>Which anchors connect, per layer and footprint, for <see cref="IsReachable"/>.</summary>
        public NavRegions Regions;

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

        /// <summary>A blank grid with every layer allocated; the caller fills the cells and disposes it.</summary>
        public static NavGrid Create(int2 size, float2 min, float cellSize, in TerrainHeight terrain, float waterLevel)
        {
            var count = size.x * size.y;
            return new NavGrid
            {
                Cells = new NativeArray<byte>(count, Allocator.Persistent),
                Scenery = new NativeArray<byte>(count, Allocator.Persistent),
                Clearance = new NativeArray<byte>(count * ClearanceLayers, Allocator.Persistent),
                DeckHeights = new NativeArray<float>(count, Allocator.Persistent),
                Regions = NavRegions.Create(count, Allocator.Persistent),
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
            Clearance.Dispose();
            DeckHeights.Dispose();
            Regions.Dispose();
            Changes.Dispose();
        }

        public readonly int2 WorldToCell(float3 position) => WorldToCell(position.xz);

        public readonly int2 WorldToCell(float2 position) => (int2)math.floor((position - Min) / CellSize);

        public readonly float3 CellCenter(int2 cell) =>
            new(Min.x + (cell.x + 0.5f) * CellSize, 0f, Min.y + (cell.y + 0.5f) * CellSize);

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

        /// <summary>True when every cell under an XZ box is inside the map and open to the layer.</summary>
        public readonly bool IsAreaFree(float3 center, float2 size, NavLayer layer = NavLayer.Ground)
        {
            GetArea(center, size, out var min, out var max);
            var surfaces = NavLayers.Surfaces(layer);
            for (var y = min.y; y <= max.y; y++)
            {
                for (var x = min.x; x <= max.x; x++)
                {
                    if (!IsOpen(new int2(x, y), surfaces))
                    {
                        return false;
                    }
                }
            }

            return true;
        }

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

        /// <summary>Closest cell open to the layer by ring distance, up to <paramref name="maxRadius"/> rings out.</summary>
        public readonly bool TryFindNearestWalkable(int2 cell, int maxRadius, out int2 found,
            NavLayer layer = NavLayer.Ground) =>
            NavAccess.Surface(this, layer).TryFindNearest(cell, maxRadius, out found);

        /// <summary>
        /// Whether an agent could path between two points: their anchors share a region, scenery aside. A start in
        /// closed cells counts from the open ground it would step out to. Aircraft reach everywhere.
        /// </summary>
        public readonly bool IsReachable(in NavAgent agent, float3 from, float3 to)
        {
            if (agent.Layer == NavLayer.Air)
            {
                return true;
            }

            var access = agent.Access(this);
            if (!access.TryFindNearest(access.AnchorAt(from), GridPathfinder.NearestSearchRadius, out var start))
            {
                return false;
            }

            var footprint = access.Footprint;
            return Region(agent.Layer, footprint, start) == Region(agent.Layer, footprint, access.AnchorAt(to));
        }

        public readonly int Region(NavLayer layer, int footprint, int2 anchor) =>
            Regions.Label(this, (int)layer, footprint, anchor);

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

        /// <summary>Builds what paths read from the cells: every layer's <see cref="Clearance"/>, then the <see cref="Regions"/>.</summary>
        public void BuildAccess()
        {
            for (var slot = 0; slot < ClearanceLayers; slot++)
            {
                BuildClearance(slot);
            }

            Regions.Build(this);
        }

        /// <summary><see cref="BuildAccess"/> on workers, one per layer and then one per region slice.</summary>
        public readonly JobHandle ScheduleAccess(JobHandle dependency)
        {
            var clearance = new ClearanceJob { Cells = Cells, Clearance = Clearance, Size = Size }
                .Schedule(ClearanceLayers, 1, dependency);
            return Regions.Schedule(this, clearance);
        }

        /// <summary>
        /// Fills one layer's <see cref="Clearance"/>: the side, in cells, of the largest open square whose low corner is
        /// the cell, so a footprint k cells wide fits wherever it is at least k. Layers touch disjoint slices.
        /// </summary>
        public void BuildClearance(int slot)
        {
            var offset = slot * Cells.Length;
            var surfaces = NavLayers.Surfaces((NavLayer)slot);
            for (var y = Size.y - 1; y >= 0; y--)
            {
                for (var x = Size.x - 1; x >= 0; x--)
                {
                    var cell = new int2(x, y);
                    var side = IsOpen(cell, surfaces) ? 1 + SmallestSquareAbove(slot, cell) : 0;
                    Clearance[offset + Index(cell)] = (byte)math.min(side, byte.MaxValue);
                }
            }
        }

        // A square grows past its neighbours' only as far as the smallest of the three it overlaps.
        private readonly int SmallestSquareAbove(int slot, int2 cell)
        {
            var right = ClearanceAt(slot, cell + new int2(1, 0));
            var up = ClearanceAt(slot, cell + new int2(0, 1));
            return math.min(math.min(right, up), ClearanceAt(slot, cell + 1));
        }

        /// <summary>A layer's <see cref="Clearance"/> at a cell; zero off the grid.</summary>
        public readonly int ClearanceAt(int slot, int2 cell) =>
            InBounds(cell) ? Clearance[slot * Cells.Length + Index(cell)] : 0;

        internal readonly bool IsOpen(int2 cell, NavSurface surfaces) =>
            InBounds(cell) && ((NavSurface)Cells[Index(cell)] & surfaces) != 0;

        /// <summary>One layer per index; each writes only its own slice of the clearance array.</summary>
        [BurstCompile]
        private struct ClearanceJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<byte> Cells;
            [NativeDisableParallelForRestriction] public NativeArray<byte> Clearance;
            public int2 Size;

            // A grid of just the fields clearance reads keeps the rest out of the job.
            public void Execute(int slot) =>
                new NavGrid { Cells = Cells, Clearance = Clearance, Size = Size }.BuildClearance(slot);
        }
    }
}
