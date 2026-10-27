using Unity.Collections;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Navigation
{
    internal partial struct DrivePlanner
    {
        private void Trace(float2 start, NativeList<float3> points)
        {
            _corners.Clear();
            _distances.Clear();
            _corners.Add(start);
            _distances.Add(0f);
            foreach (var point in points)
            {
                var step = math.distance(_corners[_corners.Length - 1], point.xz);
                if (step > 1e-3f)
                {
                    _corners.Add(point.xz);
                    _distances.Add(Total + step);
                }
            }
        }

        private readonly AxlePose PoseAt(float distance) =>
            distance <= 0f ? _start : Chassis.Pose(PointAt(distance), HeadingAt(distance));

        // A pose along the path; at its end the point, or for a pose end where its run-in starts.
        private readonly DriveGoal GoalAt(float distance)
        {
            if (distance < Total - 1e-3f)
            {
                return DriveGoal.ToPose(PointAt(distance), HeadingAt(distance));
            }

            var end = _corners[_corners.Length - 1];
            return HasPoseEnd ? DriveGoal.ToPose(end - _endFacing * _runIn, _endFacing) : DriveGoal.ToPoint(end);
        }

        private readonly float2 PointAt(float distance)
        {
            var leg = LegAt(distance);
            return _corners[leg] + HeadingAt(distance) * (distance - _distances[leg]);
        }

        private readonly float2 HeadingAt(float distance)
        {
            return LegHeading(LegAt(distance));
        }

        private readonly float2 LegHeading(int leg) => math.normalize(_corners[leg + 1] - _corners[leg]);

        private readonly int LegAt(float distance)
        {
            var leg = 0;
            while (leg < _corners.Length - 2 && _distances[leg + 1] <= distance)
            {
                leg++;
            }

            return leg;
        }

        private readonly float TurnAt(int corner)
        {
            return math.acos(math.clamp(math.dot(LegHeading(corner - 1), LegHeading(corner)), -1f, 1f));
        }
    }
}
