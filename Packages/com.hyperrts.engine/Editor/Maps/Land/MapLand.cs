using System;
using System.Collections.Generic;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Land
{
    /// <summary>
    /// Land use of the whole terrain on a 1 m grid: each cell holds the region that claimed it first. Planners claim
    /// in priority order (water, roads, bases, ravines, settlements, woods, belts, farmland, meadow), so a later
    /// claim never overwrites an earlier one.
    /// </summary>
    public sealed class MapLand
    {
        public const float CellSize = 1f;

        /// <summary>The region index of a cell nothing has claimed.</summary>
        public const int Unclaimed = 0;

        private readonly ushort[] _cells;
        private readonly List<MapLandRegion> _regions = new() { null };

        public MapLand(Rect bounds)
        {
            Bounds = bounds;
            Width = Mathf.CeilToInt(bounds.width / CellSize);
            Height = Mathf.CeilToInt(bounds.height / CellSize);
            _cells = new ushort[Width * Height];
        }

        public Rect Bounds { get; }

        public int Width { get; }

        public int Height { get; }

        /// <summary>Every region by index; index 0 is "unclaimed" and holds null.</summary>
        public IReadOnlyList<MapLandRegion> Regions => _regions;

        public int Add(MapLandRegion region)
        {
            if (_regions.Count == ushort.MaxValue)
            {
                throw new InvalidOperationException($"A map can't have more than {ushort.MaxValue - 1} land regions.");
            }

            _regions.Add(region);
            return _regions.Count - 1;
        }

        public int RegionIndex(int x, int z) => _cells[z * Width + x];

        public MapLandRegion Region(int x, int z) => _regions[_cells[z * Width + x]];

        /// <summary>The region at a map point, or null off the terrain or where nothing claimed it yet.</summary>
        public MapLandRegion At(Vector2 point) => TryCell(point, out var x, out var z) ? Region(x, z) : null;

        public MapLandKind KindAt(Vector2 point) => At(point)?.Kind ?? MapLandKind.Open;

        public bool IsFree(int x, int z) => _cells[z * Width + x] == 0;

        /// <summary>Claims a cell for a region unless something claimed it already; returns whether it did.</summary>
        public bool Claim(int x, int z, int region)
        {
            var i = z * Width + x;
            if (_cells[i] != 0)
            {
                return false;
            }

            _cells[i] = (ushort)region;
            return true;
        }

        /// <summary>Gives a claimed cell to another region, for land worn into what was laid first (a yard path).</summary>
        public void Reclaim(int x, int z, int region) => _cells[z * Width + x] = (ushort)region;

        public Vector2 Center(int x, int z) => Bounds.min + new Vector2((x + 0.5f) * CellSize, (z + 0.5f) * CellSize);

        public bool TryCell(Vector2 point, out int x, out int z)
        {
            x = Mathf.FloorToInt((point.x - Bounds.xMin) / CellSize);
            z = Mathf.FloorToInt((point.y - Bounds.yMin) / CellSize);
            return x >= 0 && z >= 0 && x < Width && z < Height;
        }

        /// <summary>Claims every free cell in an area whose centre passes the test; returns how many it took.</summary>
        public int ClaimWhere(Rect area, int region, Func<Vector2, bool> inside)
        {
            var claimed = 0;
            ForEachIn(area, (x, z, center) =>
            {
                if (IsFree(x, z) && inside(center) && Claim(x, z, region))
                {
                    claimed++;
                }
            });
            return claimed;
        }

        /// <summary>
        /// Claims every free cell in an area for the region the pick returns for its centre; <see cref="Unclaimed"/>
        /// leaves the cell free. The pick runs only for cells that are still free, so one pass can lay nested bands.
        /// </summary>
        public int ClaimEach(Rect area, Func<Vector2, int> pick)
        {
            var claimed = 0;
            ForEachIn(area, (x, z, center) =>
            {
                if (!IsFree(x, z))
                {
                    return;
                }

                var region = pick(center);
                if (region != Unclaimed && Claim(x, z, region))
                {
                    claimed++;
                }
            });
            return claimed;
        }

        /// <summary>Calls back for every cell whose centre lies in a rectangle of the map.</summary>
        public void ForEachIn(Rect area, Action<int, int, Vector2> visit)
        {
            var minX = Mathf.Max(0, Mathf.FloorToInt((area.xMin - Bounds.xMin) / CellSize));
            var minZ = Mathf.Max(0, Mathf.FloorToInt((area.yMin - Bounds.yMin) / CellSize));
            var maxX = Mathf.Min(Width - 1, Mathf.CeilToInt((area.xMax - Bounds.xMin) / CellSize));
            var maxZ = Mathf.Min(Height - 1, Mathf.CeilToInt((area.yMax - Bounds.yMin) / CellSize));
            for (var z = minZ; z <= maxZ; z++)
            {
                for (var x = minX; x <= maxX; x++)
                {
                    visit(x, z, Center(x, z));
                }
            }
        }

        /// <summary>Calls back for every cell of the terrain.</summary>
        public void ForEach(Action<int, int, Vector2> visit) => ForEachIn(Bounds, visit);
    }
}
