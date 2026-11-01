using HyperRTS.Editor.Maps.Geometry;
using HyperRTS.Editor.Maps.Land;

namespace HyperRTS.Editor.Maps.Ground
{
    /// <summary>
    /// For every land cell, the distance to the nearest cell of another region and which region that is (spread
    /// as <see cref="MapLandDistance"/> spreads distances, carrying the region along), so a border knows both of its sides.
    /// </summary>
    public sealed class MapEdgeNeighbours
    {
        private readonly MapValues<int> _own;

        public MapEdgeNeighbours(MapLand land)
        {
            var grid = land.Grid;
            Distance = new MapValues<float>(grid);
            Other = new MapValues<int>(grid);
            _own = new MapValues<int>(grid);
            for (var z = 0; z < grid.Height; z++)
            {
                for (var x = 0; x < grid.Width; x++)
                {
                    Seed(land, x, z);
                }
            }

            MapLandDistance.Spread(grid, Carry);
        }

        /// <summary>Cells to the nearest cell of another region; 0 where a cell touches one.</summary>
        public MapValues<float> Distance { get; }

        /// <summary>The region of that nearest other cell; the cell's own region on land of one region only.</summary>
        public MapValues<int> Other { get; }

        private void Seed(MapLand land, int x, int z)
        {
            var i = _own.Grid.Index(x, z);
            var own = _own[i] = land.RegionIndex(x, z);
            Distance[i] = float.MaxValue;
            Other[i] = own;
            foreach (var (dx, dz) in Sides)
            {
                var (nx, nz) = (x + dx, z + dz);
                var inside = _own.Grid.Contains(nx, nz);
                var neighbour = inside ? land.RegionIndex(nx, nz) : own;
                if (neighbour == own)
                {
                    continue;
                }

                Distance[i] = 0f;
                Other[i] = neighbour;
                return;
            }
        }

        private static readonly (int, int)[] Sides = { (-1, 0), (1, 0), (0, -1), (0, 1) };

        /// <summary>A neighbour's distance carries over only if its other region is not this cell's own.</summary>
        private void Carry(int cell, int from, float step)
        {
            var candidate = Distance[from] + step;
            if (candidate >= Distance[cell] || Other[from] == _own[cell])
            {
                return;
            }

            Distance[cell] = candidate;
            Other[cell] = Other[from];
        }
    }
}
