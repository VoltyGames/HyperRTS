using HyperRTS.Editor.Maps.Climate;
using HyperRTS.Editor.Maps.Geometry;
using HyperRTS.Editor.Maps.Land;
using HyperRTS.Editor.Maps.Layout;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Ground
{
    /// <summary>
    /// The land-use borders as the ground painters see them, so texture, grass and tint end on one line. Each border
    /// takes the crisper of its two surfaces' edge styles (a ploughed edge stays sharp against a meadow; two meadows
    /// blend wide), smoothed over a few metres so the style changes without a step where three regions meet.
    /// </summary>
    public sealed class MapEdgeField
    {
        private const int StyleBlurCells = 3;

        /// <summary>Distance past any blend band, so land of one region needs no special case.</summary>
        private const float FarInside = 64f;

        private readonly MapLand _land;
        private readonly MapEdgeWander _wander;
        private readonly MapEdgeNeighbours _neighbours;
        private readonly MapClimateSurface[] _surfaces;
        private readonly MapField<float> _width;
        private readonly MapField<float> _wanderAmount;

        public MapEdgeField(MapLayout layout, MapLand land, MapClimate climate)
        {
            _land = land;
            _wander = new MapEdgeWander(layout);
            _neighbours = new MapEdgeNeighbours(land);
            _surfaces = new MapClimateSurface[land.Regions.Count];
            _surfaces[MapLand.Unclaimed] = climate.Surface(climate.groundSurface);
            for (var i = 1; i < land.Regions.Count; i++)
            {
                _surfaces[i] = climate.Surface(land.Regions[i].Surface);
            }

            _width = Style(s => s.edgeWidth);
            _wanderAmount = Style(s => s.edgeWander);
        }

        /// <summary>The surface a region paints; unclaimed land paints the climate's ground surface.</summary>
        public MapClimateSurface Surface(int region) => _surfaces[region];

        public int RegionCount => _surfaces.Length;

        public MapLandRegion Region(int region) => _land.Regions[region];

        /// <summary>The border round a map point, looked up through the edge wander.</summary>
        public MapEdgeSample Sample(Vector2 point)
        {
            var grid = _land.Grid;
            var at = point + _wander.Offset(point, grid.Bilinear(point).Blend(_wanderAmount));
            var corners = grid.Bilinear(at);
            var (cx, cz) = corners.Nearest;
            var own = _land.RegionIndex(cx, cz);
            var other = _neighbours.Other[cx, cz];
            var inside = corners.Blend(Signed(corners.X0, corners.Z0, own), Signed(corners.X1, corners.Z0, own),
                Signed(corners.X0, corners.Z1, own), Signed(corners.X1, corners.Z1, own));
            return new MapEdgeSample(at, own, other, inside, corners.Blend(_width));
        }

        /// <summary>Per cell, the crisper of the two sides' values, blurred so it changes smoothly.</summary>
        private MapField<float> Style(System.Func<MapClimateSurface, float> value)
        {
            var cells = new MapField<float>(_land.Grid);
            for (var z = 0; z < _land.Height; z++)
            {
                for (var x = 0; x < _land.Width; x++)
                {
                    var i = cells.Grid.Index(x, z);
                    var own = value(_surfaces[_land.RegionIndex(x, z)]);
                    cells[i] = Mathf.Min(own, value(_surfaces[_neighbours.Other[i]]));
                }
            }

            return MapGridBlur.Box(cells, StyleBlurCells);
        }

        /// <summary>A cell's distance to its border measured from its edge, positive in <paramref name="own"/>.</summary>
        private float Signed(int x, int z, int own)
        {
            var distance = Mathf.Min(_neighbours.Distance[x, z], FarInside) + 0.5f * MapLand.CellSize;
            return _land.RegionIndex(x, z) == own ? distance : -distance;
        }
    }
}
