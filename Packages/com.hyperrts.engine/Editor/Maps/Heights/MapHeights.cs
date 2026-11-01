using HyperRTS.Editor.Maps.Geometry;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Heights
{
    /// <summary>World ground heights (metres) on a square grid of samples covering the map, row by row from the south.</summary>
    public sealed class MapHeights
    {
        private readonly MapField<float> _samples;

        public MapHeights(int resolution, Rect bounds)
        {
            _samples = new MapField<float>(MapGrid.Corners(bounds, resolution));
        }

        public int Resolution => _samples.Grid.Width;

        public Rect Bounds => _samples.Grid.Bounds;

        public Vector2 Spacing => _samples.Grid.Spacing;

        public float this[int x, int z]
        {
            get => _samples[x, z];
            set => _samples[x, z] = value;
        }

        public Vector2 Position(int x, int z) => _samples.Grid.Position(x, z);

        /// <summary>Bilinear height, clamped to the edge samples outside the map.</summary>
        public float Height(Vector2 point) => _samples.Grid.Bilinear(point).Blend(_samples);

        /// <summary>Ground slope in degrees, measured across <paramref name="span"/> metres.</summary>
        public float Slope(Vector2 point, float span = 2f)
        {
            var half = span * 0.5f;
            var dx = Height(point + new Vector2(half, 0f)) - Height(point - new Vector2(half, 0f));
            var dz = Height(point + new Vector2(0f, half)) - Height(point - new Vector2(0f, half));
            return Mathf.Atan(new Vector2(dx, dz).magnitude / span) * Mathf.Rad2Deg;
        }

        /// <summary>Shortest span (metres) a normal is sampled across, so small things don't catch single-sample bumps.</summary>
        public const float MinNormalSpan = 2f;

        /// <summary>Unit ground normal (world space, y up) from central differences across <paramref name="span"/> metres.</summary>
        public Vector3 Normal(Vector2 point, float span = MinNormalSpan)
        {
            span = Mathf.Max(MinNormalSpan, span);
            var half = span * 0.5f;
            var dx = Height(point + new Vector2(half, 0f)) - Height(point - new Vector2(half, 0f));
            var dz = Height(point + new Vector2(0f, half)) - Height(point - new Vector2(0f, half));
            return new Vector3(-dx, span, -dz).normalized;
        }

        public MapHeights Copy()
        {
            var copy = new MapHeights(Resolution, Bounds);
            _samples.Values.CopyTo(copy._samples.Values, 0);
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
        public void ForEachIn(Rect area, System.Action<int, int, Vector2> visit) => _samples.Grid.ForEachIn(area, visit);

        /// <summary>Keeps every height inside the terrain's range; returns how many samples had to move.</summary>
        public int Clamp(float min, float max)
        {
            var moved = 0;
            var samples = _samples.Values;
            for (var i = 0; i < samples.Length; i++)
            {
                var clamped = Mathf.Clamp(samples[i], min, max);
                if (clamped != samples[i])
                {
                    samples[i] = clamped;
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
