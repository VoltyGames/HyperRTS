using HyperRTS.Editor.Maps.Data;
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
        private readonly MapEdgeJitter _jitter;
        private readonly MapEdgeNeighbours _neighbours;
        private readonly MapClimateSurface[] _surfaces;
        private readonly float[] _width;
        private readonly float[] _jitterAmount;

        public MapEdgeField(MapLayout layout, MapLand land, MapClimate climate)
        {
            _land = land;
            _jitter = new MapEdgeJitter(layout);
            _neighbours = new MapEdgeNeighbours(land);
            _surfaces = new MapClimateSurface[land.Regions.Count];
            _surfaces[MapLand.Unclaimed] = climate.Surface(climate.groundSurface);
            for (var i = 1; i < land.Regions.Count; i++)
            {
                _surfaces[i] = climate.Surface(land.Regions[i].Surface);
            }

            _width = Style(s => s.edgeWidth);
            _jitterAmount = Style(s => s.edgeJitter);
        }

        /// <summary>The surface a region paints; unclaimed land paints the climate's ground surface.</summary>
        public MapClimateSurface Surface(int region) => _surfaces[region];

        /// <summary>The border round a map point, looked up through the edge jitter.</summary>
        public MapEdgeSample Sample(Vector2 point)
        {
            var at = point + _jitter.Offset(point, Bilinear(_jitterAmount, point));
            Corners(at, out var x0, out var z0, out var x1, out var z1, out var fx, out var fz);
            var cx = fx < 0.5f ? x0 : x1;
            var cz = fz < 0.5f ? z0 : z1;
            var own = _land.RegionIndex(cx, cz);
            var other = _neighbours.Other[cz * _land.Width + cx];
            var inside = Mathf.Lerp(
                Mathf.Lerp(Signed(x0, z0, own), Signed(x1, z0, own), fx),
                Mathf.Lerp(Signed(x0, z1, own), Signed(x1, z1, own), fx), fz);
            return new MapEdgeSample(at, own, other, inside, Bilinear(_width, at));
        }

        /// <summary>Per cell, the crisper of the two sides' values, blurred so it changes smoothly.</summary>
        private float[] Style(System.Func<MapClimateSurface, float> value)
        {
            var cells = new float[_land.Width * _land.Height];
            for (var z = 0; z < _land.Height; z++)
            {
                for (var x = 0; x < _land.Width; x++)
                {
                    var i = z * _land.Width + x;
                    var own = value(_surfaces[_land.RegionIndex(x, z)]);
                    cells[i] = Mathf.Min(own, value(_surfaces[_neighbours.Other[i]]));
                }
            }

            return MapGridBlur.Box(cells, _land.Width, _land.Height, StyleBlurCells);
        }

        /// <summary>A cell's distance to its border measured from its edge, positive in <paramref name="own"/>.</summary>
        private float Signed(int x, int z, int own)
        {
            var distance = Mathf.Min(_neighbours.Distance[z * _land.Width + x], FarInside) + 0.5f * MapLand.CellSize;
            return _land.RegionIndex(x, z) == own ? distance : -distance;
        }

        private float Bilinear(float[] field, Vector2 point)
        {
            Corners(point, out var x0, out var z0, out var x1, out var z1, out var fx, out var fz);
            var width = _land.Width;
            var near = Mathf.Lerp(field[z0 * width + x0], field[z0 * width + x1], fx);
            var far = Mathf.Lerp(field[z1 * width + x0], field[z1 * width + x1], fx);
            return Mathf.Lerp(near, far, fz);
        }

        /// <summary>The four cell centres round a point, clamped to the land, and where it sits between them.</summary>
        private void Corners(Vector2 point, out int x0, out int z0, out int x1, out int z1, out float fx, out float fz)
        {
            var grid = (point - _land.Bounds.min) / MapLand.CellSize - new Vector2(0.5f, 0.5f);
            x0 = Mathf.Clamp(Mathf.FloorToInt(grid.x), 0, _land.Width - 1);
            z0 = Mathf.Clamp(Mathf.FloorToInt(grid.y), 0, _land.Height - 1);
            x1 = Mathf.Min(x0 + 1, _land.Width - 1);
            z1 = Mathf.Min(z0 + 1, _land.Height - 1);
            fx = Mathf.Clamp01(grid.x - x0);
            fz = Mathf.Clamp01(grid.y - z0);
        }
    }
}
