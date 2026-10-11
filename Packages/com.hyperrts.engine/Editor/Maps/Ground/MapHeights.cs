using HyperRTS.Editor.Maps.Geometry;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Ground
{
    /// <summary>World ground heights (metres) on a square grid of samples covering the map, row by row from the south.</summary>
    public sealed class MapHeights
    {
        private readonly float[] _samples;

        public MapHeights(int resolution, Rect bounds)
        {
            Resolution = resolution;
            Bounds = bounds;
            _samples = new float[resolution * resolution];
        }

        public int Resolution { get; }

        public Rect Bounds { get; }

        public Vector2 Spacing => Bounds.size / (Resolution - 1);

        public float this[int x, int z]
        {
            get => _samples[z * Resolution + x];
            set => _samples[z * Resolution + x] = value;
        }

        public Vector2 Position(int x, int z) => Bounds.min + Vector2.Scale(new Vector2(x, z), Spacing);

        /// <summary>Bilinear height, clamped to the edge samples outside the map.</summary>
        public float Height(Vector2 point)
        {
            var local = (point - Bounds.min) / Spacing;
            var max = Resolution - 1;
            local.x = Mathf.Clamp(local.x, 0f, max);
            local.y = Mathf.Clamp(local.y, 0f, max);
            var x = Mathf.Min((int)local.x, max - 1);
            var z = Mathf.Min((int)local.y, max - 1);
            var tx = local.x - x;
            var tz = local.y - z;
            var bottom = Mathf.Lerp(this[x, z], this[x + 1, z], tx);
            var top = Mathf.Lerp(this[x, z + 1], this[x + 1, z + 1], tx);
            return Mathf.Lerp(bottom, top, tz);
        }

        /// <summary>Ground slope in degrees, measured across <paramref name="span"/> metres.</summary>
        public float Slope(Vector2 point, float span = 2f)
        {
            var half = span * 0.5f;
            var dx = Height(point + new Vector2(half, 0f)) - Height(point - new Vector2(half, 0f));
            var dz = Height(point + new Vector2(0f, half)) - Height(point - new Vector2(0f, half));
            return Mathf.Atan(new Vector2(dx, dz).magnitude / span) * Mathf.Rad2Deg;
        }

        public MapHeights Copy()
        {
            var copy = new MapHeights(Resolution, Bounds);
            _samples.CopyTo(copy._samples, 0);
            return copy;
        }

        /// <summary>
        /// Levels a disc to its own mean height, easing back to the old ground over <paramref name="blend"/> metres.
        /// Returns the level it chose.
        /// </summary>
        public float Flatten(Vector2 center, float radius, float blend)
        {
            var level = Mean(center, radius);
            ForEachNear(center, radius + blend, (x, z, distance) =>
            {
                var weight = 1f - MapGeometry.Smooth((distance - radius) / blend);
                this[x, z] = Mathf.Lerp(this[x, z], level, weight);
            });
            return level;
        }

        /// <summary>Calls back for every sample within <paramref name="reach"/> metres of a point, with its distance.</summary>
        public void ForEachNear(Vector2 center, float reach, System.Action<int, int, float> visit)
        {
            ForEachIn(new Rect(center - Vector2.one * reach, Vector2.one * (reach * 2f)), (x, z, position) =>
            {
                var distance = (position - center).magnitude;
                if (distance <= reach)
                {
                    visit(x, z, distance);
                }
            });
        }

        /// <summary>Calls back for every sample inside a rectangle of the map, with its position.</summary>
        public void ForEachIn(Rect area, System.Action<int, int, Vector2> visit)
        {
            var min = Vector2Int.FloorToInt((area.min - Bounds.min) / Spacing);
            var max = Vector2Int.CeilToInt((area.max - Bounds.min) / Spacing);
            for (var z = Mathf.Max(0, min.y); z <= Mathf.Min(Resolution - 1, max.y); z++)
            {
                for (var x = Mathf.Max(0, min.x); x <= Mathf.Min(Resolution - 1, max.x); x++)
                {
                    visit(x, z, Position(x, z));
                }
            }
        }

        /// <summary>Keeps every height inside the terrain's range; returns how many samples had to move.</summary>
        public int Clamp(float min, float max)
        {
            var moved = 0;
            for (var i = 0; i < _samples.Length; i++)
            {
                var clamped = Mathf.Clamp(_samples[i], min, max);
                if (clamped != _samples[i])
                {
                    _samples[i] = clamped;
                    moved++;
                }
            }

            return moved;
        }

        private float Mean(Vector2 center, float radius)
        {
            var sum = 0f;
            var count = 0;
            ForEachNear(center, radius, (x, z, _) =>
            {
                sum += this[x, z];
                count++;
            });
            return count > 0 ? sum / count : Height(center);
        }
    }
}
