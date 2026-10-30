using HyperRTS.Editor.Maps.Data;
using HyperRTS.Editor.Maps.Geometry;
using HyperRTS.Editor.Maps.Symmetry;
using HyperRTS.Presentation.Environment.RowCrops;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Ground
{
    /// <summary>
    /// The lines along each region's rows: tramlines, painted and left bare, and harvest swaths, lighter and thinner
    /// in plants. Both are measured in the sector, so every copy matches, and softened over one texel of the picture.
    /// </summary>
    public sealed class MapRowLines
    {
        private readonly MapSymmetry _symmetry;
        private readonly RowCropTramlines[] _tramlines;
        private readonly MapSwaths[] _swaths;
        private readonly float[] _swathOffset;
        private readonly Vector2[] _across;
        private readonly float _texel;

        public MapRowLines(MapSymmetry symmetry, MapEdgeField edges, float texel)
        {
            (_symmetry, _texel) = (symmetry, texel);
            var count = edges.RegionCount;
            (_tramlines, _swaths, _swathOffset) = (new RowCropTramlines[count], new MapSwaths[count], new float[count]);
            _across = new Vector2[count];
            for (var i = 1; i < count; i++)
            {
                var region = edges.Region(i);
                var surface = edges.Surface(i);
                if (!region.HasRows)
                {
                    continue;
                }

                _across[i] = MapGeometry.Right(MapGeometry.Forward(region.RowAngle));
                if (surface.tramlines.Any)
                {
                    _tramlines[i] = surface.tramlines.From(region.Seed, 0f);
                }

                if (surface.swaths.Any)
                {
                    _swaths[i] = surface.swaths;
                    _swathOffset[i] = Mathf.Repeat(region.Seed * 0.618034f, 1f) * surface.swaths.spacing;
                }
            }
        }

        /// <summary>0 off the wheel lines to 1 on them.</summary>
        public float Tramline(int region, Vector2 point)
        {
            var tramlines = _tramlines[region];
            if (!tramlines.Any)
            {
                return 0f;
            }

            var distance = tramlines.Distance(Across(region, point));
            return 1f - MapGeometry.Smooth((distance - tramlines.halfWidth) / _texel + 0.5f);
        }

        /// <summary>0 between swaths to 1 on one.</summary>
        public float Swath(int region, Vector2 point)
        {
            var swaths = _swaths[region];
            return swaths == null ? 0f : swaths.Cover(Across(region, point) + _swathOffset[region], _texel);
        }

        /// <summary>Share of standing plants a point keeps: none on a tramline, fewer on a swath.</summary>
        public float Plants(int region, Vector2 point)
        {
            var swaths = _swaths[region];
            var onSwath = swaths == null ? 0f : (1f - swaths.plants) * Swath(region, point);
            return (1f - Tramline(region, point)) * (1f - onSwath);
        }

        private float Across(int region, Vector2 point) => Vector2.Dot(_symmetry.ToSector(point, out _), _across[region]);
    }
}
