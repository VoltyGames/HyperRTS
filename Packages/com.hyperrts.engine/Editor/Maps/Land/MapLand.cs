using System;
using System.Collections.Generic;
using HyperRTS.Editor.Maps.Geometry;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Land
{
    /// <summary>
    /// Land use of the whole terrain on a 1 m grid. Planners claim cells in priority order, so a later claim never
    /// overwrites an earlier one.
    /// </summary>
    public sealed class MapLand
    {
        public const float CellSize = 1f;

        /// <summary>The region index of a cell nothing has claimed.</summary>
        public const int Unclaimed = 0;

        private readonly MapValues<ushort> _cells;
        private readonly List<MapLandRegion> _regions = new() { null };

        public MapLand(Rect bounds)
        {
            _cells = new MapValues<ushort>(MapGrid.Centres(bounds, CellSize));
        }

        public MapGrid Grid => _cells.Grid;

        public Rect Bounds => _cells.Grid.Bounds;

        public int Width => _cells.Grid.Width;

        public int Height => _cells.Grid.Height;

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

        public int RegionIndex(int x, int z) => _cells[x, z];

        public MapLandRegion Region(int x, int z) => _regions[_cells[x, z]];

        /// <summary>The region at a map point, or null off the terrain or where nothing claimed it yet.</summary>
        public MapLandRegion At(Vector2 point) => TryCell(point, out var x, out var z) ? Region(x, z) : null;

        public MapLandKind KindAt(Vector2 point) => At(point)?.Kind ?? MapLandKind.Open;

        public bool IsFree(int x, int z) => _cells[x, z] == 0;

        /// <summary>Claims a cell for a region unless something claimed it already; returns whether it did.</summary>
        public bool Claim(int x, int z, int region)
        {
            var i = _cells.Grid.Index(x, z);
            if (_cells[i] != 0)
            {
                return false;
            }

            _cells[i] = (ushort)region;
            return true;
        }

        /// <summary>Gives a claimed cell to another region, for land worn into what was laid first (a yard path).</summary>
        public void Reclaim(int x, int z, int region) => _cells[x, z] = (ushort)region;

        public Vector2 Center(int x, int z) => _cells.Grid.Position(x, z);

        /// <summary>The centre of a cell by its index in <see cref="Grid"/>.</summary>
        public Vector2 Center(int cell) => _cells.Grid.Position(cell);

        public bool TryCell(Vector2 point, out int x, out int z) => _cells.Grid.TryCell(point, out x, out z);

        /// <summary>Cells from every cell to the nearest cell of another region (0 on a region's edge).</summary>
        public MapValues<float> RegionEdgeDistance() => MapLandDistance.ToRegionEdge(_cells);

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
        public void ForEachIn(Rect area, Action<int, int, Vector2> visit) => _cells.Grid.ForEachIn(area, visit);

        /// <summary>Calls back for every cell of the terrain.</summary>
        public void ForEach(Action<int, int, Vector2> visit) => ForEachIn(Bounds, visit);
    }
}
