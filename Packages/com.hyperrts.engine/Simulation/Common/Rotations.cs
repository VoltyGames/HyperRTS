using Unity.Mathematics;

namespace HyperRTS.Simulation.Common
{
    public static class Rotations
    {
        /// <summary>The shortest rotation turning unit vector <paramref name="from"/> onto unit vector <paramref name="to"/>.</summary>
        public static quaternion FromTo(float3 from, float3 to)
        {
            var dot = math.dot(from, to);
            if (dot < -0.9999f)
            {
                return quaternion.AxisAngle(Perpendicular(from), math.PI);
            }

            return math.normalize(new quaternion(new float4(math.cross(from, to), 1f + dot)));
        }

        /// <summary>Opposite vectors have no single shortest arc: turn about +X, or +Z when the vector lies along X.</summary>
        private static float3 Perpendicular(float3 direction)
        {
            var right = math.right() - direction * direction.x;
            if (math.lengthsq(right) > 1e-6f)
            {
                return math.normalize(right);
            }

            return math.normalize(math.forward() - direction * direction.z);
        }
    }
}
