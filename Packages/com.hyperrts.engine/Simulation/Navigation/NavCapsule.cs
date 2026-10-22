using Unity.Mathematics;

namespace HyperRTS.Simulation.Navigation
{
    /// <summary>
    /// An agent's footprint on the grid: a capsule of <see cref="NavAccess.Clearance"/> round its core segment. Keeps
    /// moves and turns from pushing it deeper into cells closed to the agent; one already overlapping may always
    /// move or turn without going deeper, so it can never wedge.
    /// </summary>
    public readonly struct NavCapsule
    {
        // Moves may deepen an overlap by this much, so float noise along a wall never wedges a unit.
        internal const float Skin = 0.01f;

        public readonly NavAccess Access;

        /// <summary>Half the core segment on XZ; zero for round footprints.</summary>
        public readonly float2 Axis;

        public NavCapsule(in NavAccess access, float2 axis)
        {
            Access = access;
            Axis = axis;
        }

        /// <summary>Slides a move that would push the capsule deeper into closed cells along the wall, or rejects it.</summary>
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

            return Slide(from, to, allowed);
        }

        /// <summary>
        /// Keeps the part of the move along the wall, round its normal where the move would hit it. Along a staircase of
        /// cells the slide climbs each step's corner, so it steps back out by what it sank, never more than the move.
        /// </summary>
        private float2 Slide(float2 from, float2 to, float allowed)
        {
            var normal = WallNormal(to);
            var into = math.dot(to - from, normal);
            if (into >= 0f)
            {
                return SlideAlongAxis(from, to, allowed);
            }

            var slid = to - normal * into;
            var budget = math.length(to - from);
            var probe = Access.Grid.CellSize * 0.01f;
            for (var i = 0; i < 3; i++)
            {
                var depth = Depth(slid, Axis);
                if (depth <= allowed)
                {
                    return slid;
                }

                // Newton step back to the depth it started at, so the skin never compounds along a wall.
                var rate = math.max(0.1f, (depth - Depth(slid + normal * probe, Axis)) / probe);
                var push = math.min((depth - (allowed - Skin)) / rate, budget);
                slid += normal * push;
                budget -= push;
            }

            return SlideAlongAxis(from, to, allowed);
        }

        // Where the wall's normal says nothing useful, as wedged in a concave corner, one axis of the move may still go.
        private float2 SlideAlongAxis(float2 from, float2 to, float allowed)
        {
            var alongX = new float2(to.x, from.y);
            if (Depth(alongX, Axis) <= allowed)
            {
                return alongX;
            }

            var alongZ = new float2(from.x, to.y);
            return Depth(alongZ, Axis) <= allowed ? alongZ : from;
        }

        // Depth grows into the wall, so its downhill is the outward normal. Sampled half a cell out, a staircase of
        // cells reads as the slope it stands for.
        private float2 WallNormal(float2 at)
        {
            var h = Access.Grid.CellSize * 0.5f;
            var dx = Depth(at + new float2(h, 0f), Axis) - Depth(at - new float2(h, 0f), Axis);
            var dz = Depth(at + new float2(0f, h), Axis) - Depth(at - new float2(0f, h), Axis);
            return -math.normalizesafe(new float2(dx, dz));
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
            var grid = Access.Grid;
            var a = center + axis;
            var b = center - axis;
            var min = grid.WorldToCell(math.min(a, b) - Access.Clearance);
            var max = grid.WorldToCell(math.max(a, b) + Access.Clearance);
            var depth = 0f;
            for (var y = min.y; y <= max.y; y++)
            {
                for (var x = min.x; x <= max.x; x++)
                {
                    var cell = new int2(x, y);
                    if (Access.IsOpen(cell))
                    {
                        continue;
                    }

                    var low = grid.Min + (float2)cell * grid.CellSize;
                    var gap = SegmentRectDistance(a, b, low, low + grid.CellSize);
                    depth += math.max(0f, Access.Clearance - gap);
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
