using System.Collections.Generic;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Geometry
{
    /// <summary>
    /// A polyline with its segment lengths and directions measured once, for loops that ask for many points along it.
    /// Each lookup subtracts the cached lengths in order, so it lands exactly where measuring the line afresh would.
    /// </summary>
    public sealed class MapPathWalker
    {
        private readonly IReadOnlyList<Vector2> _line;
        private readonly float[] _lengths;
        private readonly Vector2[] _directions;

        public MapPathWalker(IReadOnlyList<Vector2> line)
        {
            _line = line;
            var segments = Mathf.Max(0, line.Count - 1);
            _lengths = new float[segments];
            _directions = new Vector2[segments];
            for (var i = 0; i < segments; i++)
            {
                var segment = line[i + 1] - line[i];
                var length = segment.magnitude;
                _lengths[i] = length;
                _directions[i] = length > 0f ? segment / length : Vector2.zero;
            }
        }

        /// <summary>The point <paramref name="distance"/> metres along the line and the line's direction there.</summary>
        public Vector2 PointAt(float distance, out Vector2 direction)
        {
            direction = Vector2.up;
            for (var i = 0; i < _lengths.Length; i++)
            {
                var length = _lengths[i];
                if (length <= 0f)
                {
                    continue;
                }

                direction = _directions[i];
                if (distance <= length || i + 1 == _lengths.Length)
                {
                    return _line[i] + direction * Mathf.Min(distance, length);
                }

                distance -= length;
            }

            return _line[_line.Count - 1];
        }
    }
}
