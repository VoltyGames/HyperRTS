using HyperRTS.Editor.Maps.Data;
using HyperRTS.Editor.Maps.Geometry;
using HyperRTS.Editor.Maps.Symmetry;
using HyperRTS.Presentation.Environment.RowCrops;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Ground
{
    /// <summary>
    /// How much of a map point the wheel lines of its region's tramlines cover, softened over one texel of the
    /// picture being painted. Lines are placed in the authored sector, as row crops place theirs.
    /// </summary>
    public sealed class MapTramlineCover
    {
        private readonly MapSymmetry _symmetry;
        private readonly RowCropTramlines[] _tramlines;
        private readonly Vector2[] _across;
        private readonly int[] _layers;
        private readonly float _halfTexel;

        public MapTramlineCover(MapSymmetry symmetry, MapEdgeField edges, MapClimate climate, float texel)
        {
            _symmetry = symmetry;
            _halfTexel = texel * 0.5f;
            _tramlines = new RowCropTramlines[edges.RegionCount];
            _across = new Vector2[edges.RegionCount];
            _layers = new int[edges.RegionCount];
            _layers[0] = -1;
            for (var i = 1; i < _tramlines.Length; i++)
            {
                var region = edges.Region(i);
                var tramlines = edges.Surface(i).tramlines;
                _layers[i] = -1;
                if (region.HasRows && tramlines.Any)
                {
                    _tramlines[i] = tramlines.From(region.Seed, 0f);
                    _across[i] = MapGeometry.Right(MapGeometry.Forward(region.RowAngle));
                    _layers[i] = climate.LayerIndex(tramlines.layer);
                }
            }
        }

        /// <summary>The terrain layer a region's wheel lines paint, or -1 where it has none.</summary>
        public int Layer(int region) => _layers[region];

        /// <summary>0 off the lines to 1 on them, within <paramref name="region"/>.</summary>
        public float Cover(int region, Vector2 point)
        {
            var tramlines = _tramlines[region];
            if (!tramlines.Any)
            {
                return 0f;
            }

            var across = Vector2.Dot(_symmetry.ToSector(point, out _), _across[region]);
            var distance = tramlines.Distance(across);
            return 1f - MapGeometry.Smooth((distance - tramlines.halfWidth + _halfTexel) / (2f * _halfTexel));
        }
    }
}
