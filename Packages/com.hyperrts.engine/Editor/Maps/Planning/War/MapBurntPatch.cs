using HyperRTS.Editor.Maps.Geometry;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Planning.War
{
    /// <summary>A patch of old burnt ground with a ragged edge, in map space.</summary>
    public readonly struct MapBurntPatch
    {
        /// <summary>How far the edge strays in and out, as a share of the radius per unit of noise.</summary>
        private const float EdgeWander = 1.5f;

        /// <summary>Metres over which the burn fades into the ground round it.</summary>
        private const float EdgeSoftness = 3f;

        public MapBurntPatch(Vector2 center, float radius)
        {
            Center = center;
            Radius = radius;
        }

        public Vector2 Center { get; }

        public float Radius { get; }

        /// <summary>Distance from the centre past which nothing is burnt.</summary>
        public float Reach => Radius * (1f + EdgeWander * 0.3f) + EdgeSoftness;

        /// <summary>Burn cover at a point, 0 to 1; <paramref name="edges"/> roughens the outline the same in every copy.</summary>
        public float Cover(Vector2 point, MapNoise edges)
        {
            var edge = Radius * (1f + EdgeWander * edges.Sample(point, Radius));
            return MapGeometry.Smooth((edge - Vector2.Distance(point, Center)) / EdgeSoftness);
        }
    }
}
