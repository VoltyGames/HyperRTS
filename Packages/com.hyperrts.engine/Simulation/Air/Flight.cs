using HyperRTS.Simulation.Common.Geometry;
using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Air
{
    /// <summary>
    /// How an <c>Air</c>-layer unit flies: cruise height, climb rate, whether it can hover, and how hard it speeds
    /// up, brakes, banks and turns its nose.
    /// </summary>
    public struct Flight : IComponentData
    {
        /// <summary>Cruise height above the ground or water surface.</summary>
        public float Altitude;

        /// <summary>Vertical speed for take-off, landing and following the terrain.</summary>
        public float ClimbSpeed;

        /// <summary>0 hovers when idle; otherwise the aircraft keeps circling at this radius (jets).</summary>
        public float LoiterRadius;

        /// <summary>Metres per second squared it gains speed at.</summary>
        public float Acceleration;

        /// <summary>Metres per second squared it sheds speed at.</summary>
        public float Braking;

        /// <summary>Radians it banks at most, which sets how tight it turns at speed.</summary>
        public float MaxBank;

        /// <summary>Radians per second its nose turns at most.</summary>
        public float YawRate;

        private const float Gravity = 9.81f;

        /// <summary>Metres per second squared it can pull across its course, banked as far as it may.</summary>
        public readonly float TurnAcceleration => Gravity * math.tan(MaxBank);

        /// <summary>Its nose after one frame of turning toward <paramref name="toward"/> at its yaw rate.</summary>
        public readonly float2 Yaw(float2 facing, float2 toward, float deltaTime)
        {
            var limit = YawRate * deltaTime;
            var turn = math.clamp(Planar.SignedAngle(facing, toward), -limit, limit);
            return math.normalizesafe(Planar.Rotate(facing, turn), facing);
        }

        /// <summary>XZ heading after flying <paramref name="distance"/> round its loiter circle.</summary>
        public readonly float2 Loiter(quaternion rotation, float distance)
        {
            var forward = math.normalizesafe(math.forward(rotation).xz, new float2(0f, 1f));
            return Planar.Rotate(forward, distance / LoiterRadius);
        }
    }
}
