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

        /// <summary>Surface height of <see cref="NavSurface.Deck"/> cells; other entries are unused.</summary>
        public NativeArray<float> DeckHeights;

        public TerrainHeight Terrain;

        /// <summary>Height ships ride at; terrain below it is water.</summary>
        public float WaterLevel;

        public int2 Size;
        public float2 Min;
        public float CellSize;

        /// <summary>Bumped on every rebuild so paths can be revalidated.</summary>
        public int Version;

        public readonly bool IsCreated => Cells.IsCreated;

        /// <summary>A one-cell stand-in for jobs in worlds without a map: jobs reject unallocated containers.</summary>
        public static NavGrid Placeholder() => new()
        {
            Cells = new NativeArray<byte>(1, Allocator.Persistent),
            DeckHeights = new NativeArray<float>(1, Allocator.Persistent),
        };

        public void Dispose()
        {
            Cells.Dispose();
            DeckHeights.Dispose();
        }

        public readonly int2 WorldToCell(float3 position) => WorldToCell(position.xz);

        public readonly int2 WorldToCell(float2 position) => (int2)math.floor((position - Min) / CellSize);

        public readonly float3 CellCenter(int2 cell) =>
            new(Min.x + (cell.x + 0.5f) * CellSize, 0f, Min.y + (cell.y + 0.5f) * CellSize);

        public readonly bool InBounds(int2 cell) => math.all(cell >= 0 & cell < Size);

        public readonly int Index(int2 cell) => cell.y * Size.x + cell.x;

        public readonly int2 Cell(int index) => new(index % Size.x, index / Size.x);

        /// <summary>Line-of-sight clearance for an agent, capped so it fits through a single-cell gap.</summary>
        public readonly float Clearance(float radius) => math.min(radius, CellSize * 0.45f);

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

        /// <summary>Closest cell open to the layer by ring distance, up to <paramref name="maxRadius"/> rings out.</summary>
        public readonly bool TryFindNearestWalkable(int2 cell, int maxRadius, out int2 found,
            NavLayer layer = NavLayer.Ground)
        {
            var surfaces = NavLayers.Surfaces(layer);
            cell = math.clamp(cell, 0, Size - 1);
            found = cell;
            if (IsOpen(cell, surfaces))
            {
                return true;
            }

            for (var r = 1; r <= maxRadius; r++)
            {
                var best = int.MaxValue;
                for (var i = -r; i <= r; i++)
                {
                    Consider(cell, new int2(i, -r), surfaces, ref best, ref found);
                    Consider(cell, new int2(i, r), surfaces, ref best, ref found);
                    Consider(cell, new int2(-r, i), surfaces, ref best, ref found);
                    Consider(cell, new int2(r, i), surfaces, ref best, ref found);
                }

                if (best != int.MaxValue)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// True when a straight move stays on cells open to the layer without cutting closed corners. A non-zero
        /// clearance also tests two parallel lines that far to each side, so smoothed paths keep off walls.
        /// </summary>
        public readonly bool HasLineOfSight(float3 from, float3 to, float clearance, NavLayer layer = NavLayer.Ground)
        {
            var surfaces = NavLayers.Surfaces(layer);
            var delta = to.xz - from.xz;
            var length = math.length(delta);
            if (clearance <= 0f || length < 1e-4f)
            {
                return IsSegmentClear(from.xz, to.xz, surfaces);
            }

            var side = new float2(-delta.y, delta.x) / length * clearance;
            return IsSegmentClear(from.xz, to.xz, surfaces) &&
                   IsSegmentClear(from.xz + side, to.xz + side, surfaces) &&
                   IsSegmentClear(from.xz - side, to.xz - side, surfaces);
        }

        internal readonly bool IsOpen(int2 cell, NavSurface surfaces) =>
            InBounds(cell) && ((NavSurface)Cells[Index(cell)] & surfaces) != 0;

        /// <summary>A diagonal step needs both side cells open, or it would slip between two closed ones.</summary>
        internal readonly bool CutsCorner(int2 from, int2 to, NavSurface surfaces)
        {
            if (from.x == to.x || from.y == to.y)
            {
                return false;
            }

            return !IsOpen(new int2(from.x, to.y), surfaces) || !IsOpen(new int2(to.x, from.y), surfaces);
        }

        private readonly void Consider(int2 center, int2 offset, NavSurface surfaces, ref int best, ref int2 found)
        {
            var distance = offset.x * offset.x + offset.y * offset.y;
            if (distance < best && IsOpen(center + offset, surfaces))
            {
                best = distance;
                found = center + offset;
            }
        }

        private readonly bool IsSegmentClear(float2 from, float2 to, NavSurface surfaces)
        {
            // Quarter-cell samples are fine enough at unit scale and far simpler than an exact grid walk.
            var steps = math.max(1, (int)math.ceil(math.distance(from, to) / (CellSize * 0.25f)));
            var previous = WorldToCell(from);
            if (!IsOpen(previous, surfaces))
            {
                return false;
            }

            for (var i = 1; i <= steps; i++)
            {
                var cell = WorldToCell(math.lerp(from, to, i / (float)steps));
                if (cell.Equals(previous))
                {
                    continue;
                }

                if (!IsOpen(cell, surfaces) || CutsCorner(previous, cell, surfaces))
                {
                    return false;
                }

                previous = cell;
            }

            return true;
        }
    }
}
