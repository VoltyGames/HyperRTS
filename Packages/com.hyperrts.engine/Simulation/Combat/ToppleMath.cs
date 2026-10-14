using HyperRTS.Simulation.Common;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.Combat
{
    /// <summary>Servers and clients both derive the remains pose from one fall heading byte, so footprints match.</summary>
    public static class ToppleMath
    {
        /// <summary>Seconds from upright until the crown hits the ground.</summary>
        public const float FallTime = 1.2f;
        public const float SettleTime = 0.3f;
        public const float Duration = FallTime + SettleTime;

        /// <summary>Bounce height as a share of the fall, and how fast it dies away.</summary>
        private const float Bounce = 0.06f;
        private const float Damping = 10f;

        /// <summary>A hit from straight above gives heading 0.</summary>
        public static byte Quantize(float3 fellFrom, float3 position) => HeadingByte.Encode(position.xz - fellFrom.xz);

        public static LocalTransform RemainsPose(in LocalTransform standing, byte fall, float lie) =>
            standing.WithRotation(quaternion.RotateY(HeadingByte.Radians(fall) - lie));

        /// <summary>The remains' final pose and the fall presentation shows on the way.</summary>
        public static Felling Fell(in LocalTransform standing, in Topple topple, byte fall, bool witnessed,
            out LocalTransform pose)
        {
            pose = RemainsPose(standing, fall, topple.Lie);
            var trunk = math.rotate(standing.Rotation, topple.Trunk);
            return new Felling { Tilt = Rotations.FromTo(HeadingByte.Direction(fall), trunk), Witnessed = witnessed };
        }

        /// <summary>The remains' rotation <paramref name="time"/> seconds into the fall to <paramref name="final"/>.</summary>
        public static quaternion Pose(in Felling felling, quaternion final, float time) =>
            math.mul(math.slerp(felling.Tilt, quaternion.identity, Progress(time)), final);

        /// <summary>0 upright, 1 down, easing in then bouncing slightly.</summary>
        public static float Progress(float time)
        {
            if (time >= Duration)
            {
                return 1f;
            }

            if (time < FallTime)
            {
                var t = math.max(time, 0f) / FallTime;
                return t * t;
            }

            var settle = time - FallTime;
            var lift = Bounce * math.exp(-Damping * settle) * math.abs(math.sin(settle / SettleTime * 2f * math.PI));
            return 1f - lift;
        }
    }
}
