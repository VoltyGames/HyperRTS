using Unity.Mathematics;

namespace HyperRTS.Simulation.Navigation
{
    /// <summary>
    /// An agent's footprint on the grid: a capsule of <see cref="NavPass.Clearance"/> round its core segment. Keeps
    /// moves and turns from pushing it deeper into cells closed to the agent; one already overlapping may always
    /// move or turn without going deeper, so it can never wedge.
    /// </summary>
    public readonly struct NavCapsule
    {
        // Moves may deepen an overlap by this much, so float noise along a wall never wedges a unit.
        private const float Skin = 0.01f;

        public readonly NavPass Pass;

        /// <summary>Half the core segment on XZ; zero for round footprints.</summary>
        public readonly float2 Axis;

        public NavCapsule(in NavPass pass, float2 axis)
        {
            Pass = pass;
            Axis = axis;
        }

        /// <summary>Rejects or slides a move that would push the capsule deeper into closed cells.</summary>
        public float2 Constrain(float2 from, float2 to)
        {
            if (to.Equals(from))
            {
                return to;
            }

            var depth = Depth(to, Axis);
            if (depth <= Skin)
            {
                return to;
            }

            var allowed = math.max(0f, Depth(from, Axis)) + Skin;
            if (depth <= allowed)
            {
                return to;
            }

            var slideX = new float2(to.x, from.y);
            if (Depth(slideX, Axis) <= allowed)
            {
                return slideX;
            }

            var slideZ = new float2(from.x, to.y);
            return Depth(slideZ, Axis) <= allowed ? slideZ : from;
        }

        /// <summary>Whether turning in place to <paramref name="axis"/> keeps both ends out of closed cells.</summary>
        public bool CanTurn(float2 center, float2 axis)
        {
            var depth = Depth(center, axis);
            return depth <= Skin || depth <= math.max(0f, Depth(center, Axis)) + Skin;
        }

        /// <summary>
        /// How far the capsule reaches into closed cells, summed over them, so sliding onto a new cell while already
        /// overlapping one still counts as deeper.
        /// </summary>
        public float Depth(float2 center, float2 axis)
        {
            var grid = Pass.Grid;
            var a = center + axis;
            var b = center - axis;
            var min = grid.WorldToCell(math.min(a, b) - Pass.Clearance);
            var max = grid.WorldToCell(math.max(a, b) + Pass.Clearance);
            var depth = 0f;
            for (var y = min.y; y <= max.y; y++)
            {
                for (var x = min.x; x <= max.x; x++)
                {
                    var cell = new int2(x, y);
                    if (Pass.IsOpen(cell))
                    {
                        continue;
                    }

                    var low = grid.Min + (float2)cell * grid.CellSize;
                    var gap = SegmentRectDistance(a, b, low, low + grid.CellSize);
                    depth += math.max(0f, Pass.Clearance - gap);
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
    }
}
