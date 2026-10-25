using Unity.Mathematics;

namespace HyperRTS.Simulation.Navigation
{
    /// <summary>
    /// An agent's footprint on the grid: a capsule of its radius round its core segment. Keeps moves and turns from
    /// pushing it deeper into walls, read from the <see cref="NavField"/>; one already overlapping may always move or
    /// turn without going deeper, so it can never wedge.
    /// </summary>
    public readonly struct NavCapsule
    {
        public readonly NavAccess Access;

        /// <summary>Half the core segment on XZ; zero for round footprints.</summary>
        public readonly float2 Axis;

        public NavCapsule(in NavAccess access, float2 axis)
        {
            Access = access;
            Axis = axis;
        }

        /// <summary>Slides a move that would push the capsule deeper into walls along them, or rejects it.</summary>
        public float2 Constrain(float2 from, float2 to)
        {
            if (to.Equals(from))
            {
                return to;
            }

            var depth = Depth(to, Axis, out var deepest);
            if (depth <= NavTolerances.Skin)
            {
                return to;
            }

            // No skin on top of an overlap, or a hull pressed into a wall frame after frame would ratchet deeper.
            var allowed = math.max(Depth(from, Axis), NavTolerances.Skin);
            if (depth <= allowed)
            {
                return to;
            }

            return Slide(from, to, Access.Normal(deepest), allowed);
        }

        // Drops the part of the move into the wall, then steps out by what it still sank, never more than the move.
        private float2 Slide(float2 from, float2 to, float2 normal, float allowed)
        {
            var slid = to - normal * math.min(0f, math.dot(to - from, normal));
            var depth = Depth(slid, Axis);
            if (depth <= allowed)
            {
                return slid;
            }

            var pushed = slid + normal * math.min(depth - allowed + NavTolerances.Skin, math.length(to - from));
            return Depth(pushed, Axis) <= allowed ? pushed : from;
        }

        /// <summary>Whether turning in place to <paramref name="axis"/> keeps both ends out of walls.</summary>
        public bool CanTurn(float2 center, float2 axis)
        {
            var depth = Depth(center, axis);
            return depth <= NavTolerances.Skin || depth <= Depth(center, Axis);
        }

        /// <summary>
        /// Where the centre may slide off the walls, at most <paramref name="reach"/>, so a turn to
        /// <paramref name="axis"/> that would swing an end into one clears it, as a track pivots off-centre.
        /// </summary>
        public bool TryTurnOff(float2 center, float2 axis, float reach, out float2 moved)
        {
            var depth = Depth(center, axis, out var deepest);
            moved = center + Access.Normal(deepest) * math.min(reach, depth + NavTolerances.Skin);
            return CanTurn(moved, axis) && Constrain(center, moved).Equals(moved);
        }

        /// <summary>
        /// The step out of the walls the capsule sinks into, at most <paramref name="maxStep"/>: down the field's
        /// slope at its sunk points, so it leaves by the nearest edge.
        /// </summary>
        public float2 WayOut(float2 center, float maxStep)
        {
            var radius = Access.Class.Radius;
            var points = Points(Axis, radius);
            var slope = float2.zero;
            var depth = 0f;
            for (var i = 0; i < points; i++)
            {
                var point = Point(center, Axis, i, points);
                var sunk = math.max(0f, radius - Access.Distance(point));
                slope += Access.Normal(point) * sunk;
                depth += sunk;
            }

            var direction = math.normalizesafe(slope);
            if (direction.Equals(float2.zero) && IsInWall(center, out var open))
            {
                direction = math.normalizesafe(Access.Grid.CellCenter(open).xz - center);
            }

            return direction * math.min(maxStep, depth + NavTolerances.Skin);
        }

        // Past the field's cap, or dead centre, a wall reads flat, so the way out is toward the nearest open cell.
        private bool IsInWall(float2 center, out int2 open)
        {
            open = default;
            if (Access.Distance(center) >= 0f)
            {
                return false;
            }

            return Access.TryFindNearest(Access.Grid.WorldToCell(center), GridPathfinder.NearestSearchRadius, out open);
        }

        /// <summary>
        /// How far the capsule reaches into walls, summed over points half a radius apart along its core, so sliding
        /// a longer stretch onto a wall counts as deeper.
        /// </summary>
        public float Depth(float2 center, float2 axis) => Depth(center, axis, out _);

        private static int Points(float2 axis, float radius) =>
            1 + (int)math.ceil(math.length(axis) * 2f / math.max(radius * 0.5f, 0.05f));

        private static float2 Point(float2 center, float2 axis, int i, int points) =>
            points == 1 ? center : center + axis * (i * 2f / (points - 1) - 1f);

        private float Depth(float2 center, float2 axis, out float2 deepest)
        {
            var radius = Access.Class.Radius;
            var points = Points(axis, radius);
            var depth = 0f;
            var most = 0f;
            deepest = center;
            for (var i = 0; i < points; i++)
            {
                var point = Point(center, axis, i, points);
                var sunk = math.max(0f, radius - Access.Distance(point));
                depth += sunk;
                if (sunk > most)
                {
                    most = sunk;
                    deepest = point;
                }
            }

            return depth;
        }

        // Apart, the nearest points are an end against the box or a box corner against the segment.
        internal static float SegmentRectDistance(float2 a, float2 b, float2 low, float2 high)
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

        internal static float PointSegmentDistance(float2 point, float2 a, float2 b)
        {
            var delta = b - a;
            var lengthSq = math.lengthsq(delta);
            var t = lengthSq > 1e-8f ? math.saturate(math.dot(point - a, delta) / lengthSq) : 0f;
            return math.distance(point, a + delta * t);
        }

        /// <summary>From one core segment to another between their closest points, each a centre and half axis.</summary>
        internal static float2 ClosestOffset(float2 position, float2 axis, float2 other, float2 otherAxis)
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
