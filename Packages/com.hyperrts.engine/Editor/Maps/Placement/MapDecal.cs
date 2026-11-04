using HyperRTS.Editor.Maps.Climate;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Placement
{
    /// <summary>One ground decal laid at build time: its kind, map position, diameter, yaw and handedness.</summary>
    public readonly struct MapDecal
    {
        public MapDecal(MapClimateDecal kind, Vector2 position, float diameter, float yaw, bool mirrored)
        {
            Kind = kind;
            Position = position;
            Diameter = diameter;
            Yaw = yaw;
            Mirrored = mirrored;
        }

        public MapClimateDecal Kind { get; }

        public Vector2 Position { get; }

        public float Diameter { get; }

        /// <summary>Degrees clockwise from north.</summary>
        public float Yaw { get; }

        /// <summary>Laid by a mirroring copy, so its texture is flipped to stay a true mirror image.</summary>
        public bool Mirrored { get; }
    }
}
