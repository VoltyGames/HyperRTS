using Unity.Mathematics;

namespace HyperRTS.Simulation.Navigation
{
    /// <summary>
    /// The shortest forward turn, straight, turn from a rear-axle pose at one turning radius (a Dubins curve), which
    /// the drive planner sweeps to ask whether a stretch can be driven as drawn.
    /// </summary>
    public struct DriveCurve
    {
        private const float FullTurn = math.PI * 2f;

        public AxlePose Start;
        public float Radius;

        /// <summary>Radians of the first turn, positive to the left.</summary>
        public float FirstTurn;

        public float Straight;

        /// <summary>Radians of the last turn, positive to the left; 0 for a curve to a point.</summary>
        public float LastTurn;

        public readonly float Length => Radius * (math.abs(FirstTurn) + math.abs(LastTurn)) + Straight;

        /// <summary>The shortest curve from one pose to another, of the four turn, straight, turn words that fit.</summary>
        public static bool TryFit(in AxlePose from, in AxlePose to, float radius, out DriveCurve curve)
        {
            curve = default;
            var found = false;
            for (var word = 0; word < 4; word++)
            {
                var first = word < 2 ? 1f : -1f;
                var last = word % 2 == 0 ? 1f : -1f;
                if (TryWord(from, to, radius, first, last, out var candidate) &&
                    (!found || candidate.Length < curve.Length))
                {
                    curve = candidate;
                    found = true;
                }
            }

            return found;
        }

        /// <summary>The shortest turn, then straight, from a pose to a point; none for a point inside both circles.</summary>
        public static bool TryFitPoint(in AxlePose from, float2 point, float radius, out DriveCurve curve)
        {
            curve = default;
            var found = false;
            for (var side = -1f; side <= 1f; side += 2f)
            {
                var center = from.Position + Left(from.Facing) * (side * radius);
                var toPoint = point - center;
                var distance = math.length(toPoint);
                if (distance < radius)
                {
                    continue;
                }

                var straight = math.sqrt(distance * distance - radius * radius);
                var heading = Rotate(toPoint / distance, side * math.atan2(radius, straight));
                var candidate = new DriveCurve
                {
                    Start = from, Radius = radius, FirstTurn = Sweep(from.Facing, heading, side), Straight = straight,
                };
                if (!found || candidate.Length < curve.Length)
                {
                    curve = candidate;
                    found = true;
                }
            }

            return found;
        }

        /// <summary>The rear axle's pose <paramref name="distance"/> metres along the curve.</summary>
        public readonly AxlePose Sample(float distance)
        {
            var left = math.clamp(distance, 0f, Length);
            var first = Radius * math.abs(FirstTurn);
            var pose = Start.Advance(Curvature(FirstTurn), math.min(left, first));
            if (left <= first)
            {
                return pose;
            }

            left -= first;
            pose = pose.Advance(0f, math.min(left, Straight));
            return left <= Straight ? pose : pose.Advance(Curvature(LastTurn), left - Straight);
        }

        private readonly float Curvature(float turn) => turn >= 0f ? 1f / Radius : -1f / Radius;

        // Each pose's circle on its side; the straight runs along a tangent common to both.
        private static bool TryWord(in AxlePose from, in AxlePose to, float radius, float first, float last,
            out DriveCurve curve)
        {
            curve = default;
            var start = from.Position + Left(from.Facing) * (first * radius);
            var end = to.Position + Left(to.Facing) * (last * radius);
            var between = end - start;
            var distance = math.length(between);
            var heading = to.Facing;
            var straight = 0f;
            if (first != last)
            {
                if (distance < radius * 2f)
                {
                    return false;
                }

                straight = math.sqrt(distance * distance - 4f * radius * radius);
                heading = Rotate(between / distance, first * math.atan2(radius * 2f, straight));
            }
            else if (distance > 1e-4f)
            {
                heading = between / distance;
                straight = distance;
            }

            curve = new DriveCurve
            {
                Start = from, Radius = radius, FirstTurn = Sweep(from.Facing, heading, first), Straight = straight,
                LastTurn = Sweep(heading, to.Facing, last),
            };
            return true;
        }

        // Radians turned from one heading to another going one way round; float noise near none reads as none.
        private static float Sweep(float2 from, float2 to, float side)
        {
            var angle = side * math.atan2(from.x * to.y - from.y * to.x, math.dot(from, to));
            if (angle < 0f)
            {
                angle += FullTurn;
            }

            return side * (angle > FullTurn - 1e-4f ? 0f : angle);
        }

        private static float2 Left(float2 facing) => new(-facing.y, facing.x);

        private static float2 Rotate(float2 direction, float angle)
        {
            math.sincos(angle, out var sin, out var cos);
            return new float2(direction.x * cos - direction.y * sin, direction.x * sin + direction.y * cos);
        }
    }
}
