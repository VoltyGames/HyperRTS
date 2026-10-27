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

        // The path's end is a point; anywhere else a pose along the path.
        private readonly DriveGoal GoalAt(float distance) =>
            distance >= Total - 1e-3f
                ? DriveGoal.ToPoint(_corners[_corners.Length - 1])
                : DriveGoal.ToPose(PointAt(distance), HeadingAt(distance));

        private readonly float2 PointAt(float distance)
        {
            var leg = LegAt(distance);
            return _corners[leg] + HeadingAt(distance) * (distance - _distances[leg]);
        }

        private readonly float2 HeadingAt(float distance)
        {
            var leg = LegAt(distance);
            return math.normalize(_corners[leg + 1] - _corners[leg]);
        }

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
            var into = math.normalize(_corners[corner] - _corners[corner - 1]);
            var outOf = math.normalize(_corners[corner + 1] - _corners[corner]);
            return math.acos(math.clamp(math.dot(into, outOf), -1f, 1f));
        }
    }
}
