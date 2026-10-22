using Unity.Collections;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Units
{
    /// <summary>
    /// The ORCA half-planes one unit must keep its velocity in, and the linear programs that pick the velocity closest
    /// to the preferred one (van den Berg et al., RVO2).
    /// </summary>
    internal struct VelocityPlanes
    {
        private const float Parallel = 1e-5f;

        private FixedList512Bytes<VelocityPlane> _planes;

        public void Add(in VelocityPlane plane) => _planes.Add(plane);

        public readonly float2 Solve(float2 preferred, float maxSpeed)
        {
            var result = float2.zero;
            var failed = Optimise(preferred, maxSpeed, false, ref result);
            if (failed < _planes.Length)
            {
                Relax(failed, maxSpeed, ref result);
            }

            return result;
        }

        /// <summary>Closest velocity to the optimum inside every plane and the speed circle; else the plane that failed.</summary>
        private readonly int Optimise(float2 optimum, float maxSpeed, bool isDirection, ref float2 result)
        {
            result = Start(optimum, maxSpeed, isDirection);
            for (var i = 0; i < _planes.Length; i++)
            {
                if (_planes[i].Violation(result) <= 0f)
                {
                    continue;
                }

                var kept = result;
                if (!OptimiseOn(i, optimum, maxSpeed, isDirection, ref result))
                {
                    result = kept;
                    return i;
                }
            }

            return _planes.Length;
        }

        private static float2 Start(float2 optimum, float maxSpeed, bool isDirection)
        {
            if (isDirection)
            {
                return optimum * maxSpeed;
            }

            return math.lengthsq(optimum) > maxSpeed * maxSpeed ? math.normalize(optimum) * maxSpeed : optimum;
        }

        /// <summary>Best velocity on plane <paramref name="index"/>'s edge within the circle and the planes before it.</summary>
        private readonly bool OptimiseOn(int index, float2 optimum, float maxSpeed, bool isDirection, ref float2 result)
        {
            var plane = _planes[index];
            var along = math.dot(plane.Point, plane.Direction);
            var discriminant = along * along + maxSpeed * maxSpeed - math.lengthsq(plane.Point);
            if (discriminant < 0f)
            {
                return false;
            }

            var low = -along - math.sqrt(discriminant);
            var high = -along + math.sqrt(discriminant);
            for (var i = 0; i < index; i++)
            {
                var other = _planes[i];
                var denominator = VelocityPlane.Cross(plane.Direction, other.Direction);
                var numerator = VelocityPlane.Cross(other.Direction, plane.Point - other.Point);
                if (math.abs(denominator) <= Parallel)
                {
                    if (numerator < 0f)
                    {
                        return false;
                    }

                    continue;
                }

                var t = numerator / denominator;
                high = denominator >= 0f ? math.min(high, t) : high;
                low = denominator < 0f ? math.max(low, t) : low;
                if (low > high)
                {
                    return false;
                }
            }

            result = plane.Point + plane.Direction * Pick(plane, optimum, isDirection, low, high);
            return true;
        }

        private static float Pick(in VelocityPlane plane, float2 optimum, bool isDirection, float low, float high)
        {
            if (isDirection)
            {
                return math.dot(optimum, plane.Direction) > 0f ? high : low;
            }

            return math.clamp(math.dot(plane.Direction, optimum - plane.Point), low, high);
        }

        /// <summary>No velocity meets every plane, as in a dense crowd: minimise the worst violation instead.</summary>
        private readonly void Relax(int failed, float maxSpeed, ref float2 result)
        {
            var worst = 0f;
            for (var i = failed; i < _planes.Length; i++)
            {
                var plane = _planes[i];
                if (plane.Violation(result) <= worst)
                {
                    continue;
                }

                var projected = Project(i);
                var kept = result;
                var inward = new float2(-plane.Direction.y, plane.Direction.x);
                if (projected.Optimise(inward, maxSpeed, true, ref result) < projected._planes.Length)
                {
                    result = kept;
                }

                worst = plane.Violation(result);
            }
        }

        /// <summary>The planes before <paramref name="index"/> as bounds on how far past its edge to go.</summary>
        private readonly VelocityPlanes Project(int index)
        {
            var plane = _planes[index];
            var projected = new VelocityPlanes();
            for (var j = 0; j < index; j++)
            {
                var other = _planes[j];
                var determinant = VelocityPlane.Cross(plane.Direction, other.Direction);
                if (math.abs(determinant) <= Parallel && math.dot(plane.Direction, other.Direction) > 0f)
                {
                    continue;
                }

                var point = math.abs(determinant) <= Parallel
                    ? 0.5f * (plane.Point + other.Point)
                    : plane.Point + plane.Direction *
                    (VelocityPlane.Cross(other.Direction, plane.Point - other.Point) / determinant);
                var direction = math.normalize(other.Direction - plane.Direction);
                projected.Add(new VelocityPlane { Point = point, Direction = direction });
            }

            return projected;
        }
    }
}
