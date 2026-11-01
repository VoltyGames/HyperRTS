using HyperRTS.Simulation.Navigation.Grid;
using HyperRTS.Simulation.Navigation.Paths;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Navigation.Agents
{
    /// <summary>
    /// An agent's footprint on the grid: a capsule of its radius round its core segment, kept from moving or turning
    /// deeper into walls. One already overlapping may still move, so it never wedges.
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

            var depth = Depth(to, Axis, Access.Class.Radius, float.PositiveInfinity, out var deepest);
            if (depth <= NavTolerances.WallOverlap)
            {
                return to;
            }

            // No skin on top of an overlap, or a hull pressed into a wall frame after frame would ratchet deeper.
            var allowed = math.max(Depth(from, Axis), NavTolerances.WallOverlap);
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

            var pushed = slid + normal * math.min(depth - allowed + NavTolerances.WallOverlap, math.length(to - from));
            return Depth(pushed, Axis) <= allowed ? pushed : from;
        }

        /// <summary>Whether turning in place to <paramref name="axis"/> keeps both ends out of walls.</summary>
        public bool CanTurn(float2 center, float2 axis)
        {
            var depth = Depth(center, axis);
            return depth <= NavTolerances.WallOverlap || depth <= Depth(center, Axis);
        }

        /// <summary>
        /// Where the centre may slide off the walls, at most <paramref name="reach"/>, so a turn to
        /// <paramref name="axis"/> that would swing an end into one clears it, as a track pivots off-centre.
        /// </summary>
        public bool TryTurnOff(float2 center, float2 axis, float reach, out float2 moved)
        {
            var depth = Depth(center, axis, Access.Class.Radius, float.PositiveInfinity, out var deepest);
            moved = center + Access.Normal(deepest) * math.min(reach, depth + NavTolerances.WallOverlap);
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
                direction = math.normalizesafe(Access.Grid.Frame.Center(open) - center);
            }

            return direction * math.min(maxStep, depth + NavTolerances.WallOverlap);
        }

        // Past the field's cap, or dead centre, a wall reads flat, so the way out is toward the nearest open cell.
        private bool IsInWall(float2 center, out int2 open)
        {
            open = default;
            if (Access.Distance(center) >= 0f)
            {
                return false;
            }

            return Access.TryFindNearest(Access.Grid.Frame.Cell(center), GridPathfinder.NearestSearchRadius, out open);
        }

        /// <summary>
        /// How far the capsule reaches into walls, summed over points half a radius apart along its core, so sliding
        /// a longer stretch onto a wall counts as deeper.
        /// </summary>
        public float Depth(float2 center, float2 axis) =>
            Depth(center, axis, Access.Class.Radius, float.PositiveInfinity, out _);

        /// <summary>
        /// The same for the capsule grown by <paramref name="clearance"/>, as a planner keeps room for error, stopping
        /// once past <paramref name="limit"/>, as it only asks whether the hull fits.
        /// </summary>
        public float Depth(float2 center, float2 axis, float clearance, float limit) =>
            Depth(center, axis, Access.Class.Radius + clearance, limit, out _);

        internal static int Points(float2 axis, float radius) =>
            1 + (int)math.ceil(math.length(axis) * 2f / math.max(radius * 0.5f, 0.05f));

        internal static float2 Point(float2 center, float2 axis, int i, int points) =>
            points == 1 ? center : center + axis * (i * 2f / (points - 1) - 1f);

        /// <summary>
        /// The most the field can change from one point to the next: along the core it changes by at most |x| + |z| per
        /// metre, so room at one point clears the next few.
        /// </summary>
        internal static float Reach(float2 axis, int points) =>
            points == 1 ? 1f : 2f * math.csum(math.abs(axis)) / (points - 1);

        private float Depth(float2 center, float2 axis, float radius, float limit, out float2 deepest) =>
            Depth(Access, center, axis, radius, limit, out deepest);

        // Skips ahead past points that room at one point clears; stops once past the limit.
        internal static float Depth<T>(in T walls, float2 center, float2 axis, float radius, float limit,
            out float2 deepest) where T : struct, IWallDistance
        {
            var points = Points(axis, radius);
            var reach = Reach(axis, points);
            var depth = 0f;
            var most = 0f;
            deepest = center;
            for (var i = 0; i < points && depth <= limit;)
            {
                var point = Point(center, axis, i, points);
                var room = walls.Distance(point) - radius;
                if (room >= 0f)
                {
                    i += 1 + (int)math.min(room / reach, points);
                    continue;
                }

                depth -= room;
                if (-room > most)
                {
                    most = -room;
                    deepest = point;
                }

                i++;
            }

            return depth;
        }
    }
}
