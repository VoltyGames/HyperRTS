using HyperRTS.Editor.Maps.Spec;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Layout
{
    /// <summary>One copy of the authored sector: a rotation about the centre or a mirror across an axis.</summary>
    public readonly struct MapTransform
    {
        private readonly float _m00;
        private readonly float _m01;
        private readonly float _m10;
        private readonly float _m11;
        private readonly float _yawOffset;

        private MapTransform(float m00, float m01, float m10, float m11, float yawOffset, bool mirrored)
        {
            _m00 = m00;
            _m01 = m01;
            _m10 = m10;
            _m11 = m11;
            _yawOffset = yawOffset;
            Mirrored = mirrored;
        }

        public static MapTransform Identity => new(1f, 0f, 0f, 1f, 0f, false);

        /// <summary>Mirrored copies reverse handedness, so yaw turns the other way.</summary>
        public bool Mirrored { get; }

        public bool IsIdentity => _m00 == 1f && _m11 == 1f && _m01 == 0f && _m10 == 0f;

        /// <summary>Counter-clockwise (seen from above) by <paramref name="degrees"/>; yaw is clockwise, so it drops.</summary>
        public static MapTransform Rotation(float degrees)
        {
            var radians = degrees * Mathf.Deg2Rad;
            var cos = Round(Mathf.Cos(radians));
            var sin = Round(Mathf.Sin(radians));
            return new MapTransform(cos, -sin, sin, cos, -degrees, false);
        }

        /// <summary>Across the line x = 0: east becomes west.</summary>
        public static MapTransform MirrorX => new(-1f, 0f, 0f, 1f, 0f, true);

        /// <summary>Across the line z = 0: north becomes south.</summary>
        public static MapTransform MirrorZ => new(1f, 0f, 0f, -1f, 180f, true);

        public Vector2 Apply(Vector2 point) => new(_m00 * point.x + _m01 * point.y, _m10 * point.x + _m11 * point.y);

        /// <summary>The point this copy moves onto <paramref name="point"/> (rotations and mirrors invert by transposing).</summary>
        public Vector2 ApplyInverse(Vector2 point) => new(_m00 * point.x + _m10 * point.y, _m01 * point.x + _m11 * point.y);

        public MapPoint Apply(MapPoint point)
        {
            var moved = Apply(point.ToVector2());
            return new MapPoint(moved.x, moved.y);
        }

        public float ApplyYaw(float yaw) => Mathf.Repeat(Mirrored ? _yawOffset - yaw : yaw + _yawOffset, 360f);

        // Exact zeros and ones keep 90° and 180° copies free of float drift.
        private static float Round(float value) => Mathf.Abs(value) < 1e-6f ? 0f : value;
    }
}
