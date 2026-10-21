using HyperRTS.Editor.Maps.Geometry;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Ground
{
    /// <summary>
    /// Where a point sits on a land-use border, after the edge wander: the region it falls in, the region across the
    /// nearest border, its signed distance into its own region and the band the two blend over.
    /// </summary>
    public readonly struct MapEdgeSample
    {
        public MapEdgeSample(Vector2 point, int own, int other, float inside, float width)
        {
            Point = point;
            Own = own;
            Other = other;
            Inside = inside;
            Width = width;
        }

        /// <summary>Where the land was looked up: the map point moved by the edge wander.</summary>
        public Vector2 Point { get; }

        public int Own { get; }

        /// <summary>The region across the nearest border; <see cref="Own"/> on land of one region.</summary>
        public int Other { get; }

        /// <summary>Metres into <see cref="Own"/> from the border; slightly negative just over it.</summary>
        public float Inside { get; }

        /// <summary>Width of the blend band across the border (metres).</summary>
        public float Width { get; }

        /// <summary>Share of <see cref="Own"/>: a half at the border, all of it half a band in.</summary>
        public float OwnShare => MapGeometry.Smooth(0.5f + Inside / Width);

        /// <summary>The region holding the point where the band crosses its middle, as grass and tint see it.</summary>
        public int Region => Inside >= 0f ? Own : Other;
    }
}
