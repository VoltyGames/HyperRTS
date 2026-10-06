using Unity.Mathematics;

namespace HyperRTS.Simulation.Air
{
    /// <summary>Altitude and loiter steps for <see cref="Flight"/>, used by movement.</summary>
    public static class FlightMath
    {
        /// <summary>
        /// Height after one climb step toward cruise altitude over the surface (the surface itself when landed),
        /// never below the surface.
        /// </summary>
        public static float Climb(float height, float surface, in Flight flight, bool landed, float deltaTime)
        {
            var goal = landed ? surface : surface + flight.Altitude;
            var step = flight.ClimbSpeed * deltaTime;
            return math.max(surface, height + math.clamp(goal - height, -step, step));
        }

        /// <summary>XZ heading after flying <paramref name="distance"/> along a circle of <paramref name="radius"/>.</summary>
        public static float2 Loiter(quaternion rotation, float distance, float radius)
        {
            var forward = math.normalizesafe(math.forward(rotation).xz, new float2(0f, 1f));
            math.sincos(distance / radius, out var sin, out var cos);
            return new float2(forward.x * cos - forward.y * sin, forward.x * sin + forward.y * cos);
        }
    }
}
