using Unity.Mathematics;

namespace HyperRTS.Simulation.Common.Geometry
{
    /// <summary>Direction and segment math on the XZ plane, angles counter-clockwise from x toward y.</summary>
    public static class Planar
    {
        /// <summary>The direction a quarter turn to the left.</summary>
        public static float2 Left(float2 direction) => new(-direction.y, direction.x);

        /// <summary>The 2D cross product: positive when <paramref name="b"/> lies to the left of <paramref name="a"/>.</summary>
        public static float Cross(float2 a, float2 b) => a.x * b.y - a.y * b.x;

        /// <summary>Radians from one direction to another, positive to the left.</summary>
        public static float SignedAngle(float2 from, float2 to) => math.atan2(Cross(from, to), math.dot(from, to));

        public static float2 Rotate(float2 direction, float angle)
        {
            math.sincos(angle, out var sin, out var cos);
            return Rotate(direction, sin, cos);
        }

        public static float2 Rotate(float2 direction, float sin, float cos) =>
            new(direction.x * cos - direction.y * sin, direction.x * sin + direction.y * cos);

        /// <summary>Zero when the segment touches the box; else the gap, from an end or a box corner.</summary>
        public static float SegmentRectDistance(float2 a, float2 b, float2 low, float2 high)
        {
            if (SegmentHitsRect(a, b, low, high))
            {
                return 0f;
            }

            var ends = math.min(PointRectDistance(a, low, high), PointRectDistance(b, low, high));
            var lowCorners = math.min(PointSegmentDistance(low, a, b), PointSegmentDistance(high, a, b));
            var highCorners = math.min(PointSegmentDistance(new float2(low.x, high.y), a, b),
                PointSegmentDistance(new float2(high.x, low.y), a, b));
            return math.min(ends, math.min(lowCorners, highCorners));
        }

        // Slab clipping (Liang and Barsky): the segment hits the box while its entry stays before its exit.
        private static bool SegmentHitsRect(float2 a, float2 b, float2 low, float2 high)
        {
            var delta = b - a;
            var enter = 0f;
            var exit = 1f;
            for (var i = 0; i < 2; i++)
            {
                if (math.abs(delta[i]) < 1e-6f)
                {
                    if (a[i] < low[i] || a[i] > high[i])
                    {
                        return false;
                    }

                    continue;
                }

                var toLow = (low[i] - a[i]) / delta[i];
                var toHigh = (high[i] - a[i]) / delta[i];
                enter = math.max(enter, math.min(toLow, toHigh));
                exit = math.min(exit, math.max(toLow, toHigh));
                if (enter > exit)
                {
                    return false;
                }
            }

            return true;
        }

        private static float PointRectDistance(float2 point, float2 low, float2 high) =>
            math.distance(point, math.clamp(point, low, high));

        public static float PointSegmentDistance(float2 point, float2 a, float2 b)
        {
            var delta = b - a;
            var lengthSq = math.lengthsq(delta);
            var t = lengthSq > 1e-8f ? math.saturate(math.dot(point - a, delta) / lengthSq) : 0f;
            return math.distance(point, a + delta * t);
        }

        /// <summary>From one core segment to another between their closest points, each a centre and half axis.</summary>
        public static float2 ClosestOffset(float2 position, float2 axis, float2 other, float2 otherAxis)
        {
            // Segments P(s) and Q(t), s and t in -1..1 (Ericson, Real-Time Collision Detection 5.1.9).
            var r = position - other;
            var a = math.lengthsq(axis);
            var e = math.lengthsq(otherAxis);
            var b = math.dot(axis, otherAxis);
            var c = math.dot(axis, r);
            var f = math.dot(otherAxis, r);
            var denominator = a * e - b * b;
            var s = denominator > 1e-6f ? math.clamp((b * f - c * e) / denominator, -1f, 1f) : 0f;
            var t = e > 1e-6f ? (b * s + f) / e : 0f;
            if (t < -1f || t > 1f)
            {
                t = math.clamp(t, -1f, 1f);
                s = a > 1e-6f ? math.clamp((b * t - c) / a, -1f, 1f) : 0f;
            }
            else if (a > 1e-6f && e <= 1e-6f)
            {
                s = math.clamp(-c / a, -1f, 1f);
            }

            return r + axis * s - otherAxis * t;
        }
    }
}
