using HyperRTS.Simulation.Navigation.Agents;
using HyperRTS.Simulation.Navigation.Grid;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Navigation.Drive
{
    /// <summary>
    /// A vehicle's hull as drive planning sees it: how deep it sits in walls, whether a curve clears them and the
    /// curves its chassis can drive.
    /// </summary>
    public readonly struct DriveHull
    {
        /// <summary>Room kept off walls, so following the plan a little wide never brushes one.</summary>
        public const float Clearance = 0.25f;

        private const float SweepStep = 0.5f;

        // Plans steer no tighter than this share of full lock, leaving the follower room to correct.
        private const float LockShare = 0.85f;

        public readonly NavAccess Access;
        public readonly Chassis Chassis;
        public readonly float HalfSegment;

        public DriveHull(in NavAccess access, in NavAgent agent)
        {
            Access = access;
            Chassis = agent.Chassis;
            HalfSegment = agent.HalfSegment;
        }

        /// <summary>The radius of the curves it fits: a little over the turn radius, or one hull length for tracks.</summary>
        public float CurveRadius => 1f / MostCurvature;

        /// <summary>The tightest curvature a plan steers.</summary>
        public float MostCurvature => Chassis.IsWheeled ? Chassis.MaxCurvature * LockShare : 1f / HullLength;

        public float HullLength => (Access.Class.Radius + HalfSegment) * 2f;

        /// <summary>
        /// How far the hull, with some room for error round it, reaches into walls with its rear axle at
        /// <paramref name="pose"/>, counted only until past <paramref name="limit"/>.
        /// </summary>
        public float Depth(in AxlePose pose, float limit) => Depth(Access, pose, limit);

        /// <summary>The same depth over other walls, such as a search's window of the field.</summary>
        public float Depth<T>(in T walls, in AxlePose pose, float limit) where T : struct, IWallDistance =>
            NavCapsule.Depth(walls, Chassis.Center(pose), pose.Facing * HalfSegment, Access.Class.Radius + Clearance,
                limit, out _);

        /// <summary>
        /// Whether the hull, swept along the first <paramref name="length"/> metres of the curve, never sinks into
        /// walls deeper than <paramref name="depth"/> or than it already has since.
        /// </summary>
        public bool IsClear(in DriveCurve curve, float length, float depth) => IsClear(Access, curve, length, depth);

        /// <summary>The same sweep over other walls, every half metre.</summary>
        public bool IsClear<T>(in T walls, in DriveCurve curve, float length, float depth)
            where T : struct, IWallDistance
        {
            var allowed = math.max(depth, NavTolerances.WallOverlap);
            for (var distance = SweepStep; distance < length + SweepStep; distance += SweepStep)
            {
                var pose = curve.Sample(math.min(distance, length));
                var sunk = Depth(walls, pose, allowed);
                if (sunk > allowed)
                {
                    return false;
                }

                allowed = math.max(sunk, NavTolerances.WallOverlap);
            }

            return true;
        }

        /// <summary>
        /// The forward curve to the goal, ending with the centre on a point goal, so the rear axle stops a rear axle
        /// short of it on the straight.
        /// </summary>
        public bool TryFit(in AxlePose from, in DriveGoal goal, out DriveCurve curve, out float length)
        {
            if (goal.HasFacing)
            {
                var fits = DriveCurve.TryFit(from, Chassis.Pose(goal.Center, goal.Facing), CurveRadius, out curve);
                length = curve.Length;
                return fits;
            }

            var reaches = DriveCurve.TryFitPoint(from, goal.Center, CurveRadius, out curve);
            length = curve.Length - Chassis.RearAxle;
            return reaches && curve.Straight >= Chassis.RearAxle;
        }
    }
}
