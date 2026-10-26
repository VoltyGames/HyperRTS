using Unity.Mathematics;

namespace HyperRTS.Simulation.Navigation
{
    /// <summary>
    /// How a hull moves over the ground: wheels roll the rear axle along arcs no tighter than <see cref="TurnRadius"/>,
    /// tracks pivot about the centre. Read by the path planner and locomotion alike.
    /// </summary>
    public struct Chassis
    {
        /// <summary>Metres from the steering axles' midpoint to the rear axle; 0 for tracks.</summary>
        public float Wheelbase;

        /// <summary>Metres the rear axle (the fixed axles' midpoint) sits behind the hull centre; 0 for tracks.</summary>
        public float RearAxle;

        /// <summary>Tightest circle of the rear axle; 0 turns at any speed.</summary>
        public float TurnRadius;

        /// <summary>Turns in place about the centre, as tracks do.</summary>
        public bool Pivots;

        /// <summary>Longest reverse leg in metres; 0 never reverses.</summary>
        public float ReverseRun;

        /// <summary>Rolls on wheels that can't turn it standing.</summary>
        public readonly bool IsWheeled => !Pivots && TurnRadius > 0f;

        /// <summary>Tightest curvature in 1/m; unlimited without a turn radius.</summary>
        public readonly float MaxCurvature => TurnRadius > 0f ? 1f / TurnRadius : float.PositiveInfinity;

        /// <summary>Metres a radian of turn costs: the turn radius, or half the hull for one that pivots.</summary>
        public readonly float TurnLength(float halfLength) => Pivots ? halfLength : TurnRadius;

        public readonly AxlePose Pose(float2 center, float2 facing) => new(center - facing * RearAxle, facing);

        public readonly float2 Center(in AxlePose pose) => pose.Position + pose.Facing * RearAxle;

        public readonly float2 FrontAxle(in AxlePose pose) => pose.Position + pose.Facing * Wheelbase;

        /// <summary>
        /// Rolls the rear axle <paramref name="length"/> metres (negative in reverse) along the arc of
        /// <paramref name="curvature"/>, positive to the left.
        /// </summary>
        public readonly AxlePose Advance(in AxlePose pose, float curvature, float length)
        {
            var turn = curvature * length;
            if (math.abs(turn) < 1e-5f)
            {
                return new AxlePose(pose.Position + pose.Facing * length, pose.Facing);
            }

            math.sincos(turn, out var sin, out var cos);
            var left = Left(pose.Facing);
            var moved = (pose.Facing * sin + left * (1f - cos)) / curvature;
            return new AxlePose(pose.Position + moved, Rotate(pose.Facing, sin, cos));
        }

        /// <summary>Turns the hull <paramref name="angle"/> radians about its centre, as tracks pivot.</summary>
        public readonly AxlePose Rotate(in AxlePose pose, float angle)
        {
            math.sincos(angle, out var sin, out var cos);
            return Pose(Center(pose), math.normalize(Rotate(pose.Facing, sin, cos)));
        }

        /// <summary>
        /// Whether no forward arc brings the hull centre to <paramref name="target"/>, so it must make a three-point
        /// turn first. The centre's tightest circle is wider than the rear axle's by the axle's offset.
        /// </summary>
        public readonly bool IsInsideTurn(float2 center, float2 facing, float2 target, float slack)
        {
            if (!IsWheeled)
            {
                return false;
            }

            var rear = center - facing * RearAxle;
            var side = Cross(facing, target - rear) >= 0f ? 1f : -1f;
            var middle = rear + Left(facing) * (side * TurnRadius);
            var reach = math.sqrt(TurnRadius * TurnRadius + RearAxle * RearAxle);
            return math.distance(target, middle) < reach * slack;
        }

        private static float2 Left(float2 facing) => new(-facing.y, facing.x);

        private static float2 Rotate(float2 facing, float sin, float cos) =>
            new(facing.x * cos - facing.y * sin, facing.x * sin + facing.y * cos);

        private static float Cross(float2 a, float2 b) => a.x * b.y - a.y * b.x;
    }
}
