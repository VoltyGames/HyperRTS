using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.Combat
{
    /// <summary>
    /// Where a <see cref="Topple"/> piece falls: a heading byte away from its lethal hit, which servers send to
    /// clients, and the pose of its remains, which both derive from that byte so the footprints match.
    /// </summary>
    public static class ToppleMath
    {
        public const int Headings = 256;

        /// <summary>Seconds from upright until the crown hits the ground, then the bounce settles.</summary>
        public const float FallTime = 1.2f;
        public const float SettleTime = 0.3f;
        public const float Duration = FallTime + SettleTime;

        /// <summary>Bounce height as a share of the whole fall, and how fast it dies away.</summary>
        private const float Bounce = 0.06f;
        private const float Damping = 10f;

        /// <summary>The heading away from <paramref name="fellFrom"/>; a hit from straight above gives heading 0.</summary>
        public static byte Quantize(float3 fellFrom, float3 position)
        {
            var away = position.xz - fellFrom.xz;
            var turns = math.atan2(away.x, away.y) / (2f * math.PI);
            return (byte)((int)math.round(turns * Headings) & (Headings - 1));
        }

        /// <summary>Radians from +Z toward +X.</summary>
        public static float Heading(byte fall) => fall * (2f * math.PI / Headings);

        public static float3 Direction(byte fall)
        {
            math.sincos(Heading(fall), out var sin, out var cos);
            return new float3(sin, 0f, cos);
        }

        /// <summary>Remains where the piece stood, turned so the crown points along the fall.</summary>
        public static LocalTransform RemainsPose(in LocalTransform standing, byte fall, float lie) =>
            standing.WithRotation(quaternion.RotateY(Heading(fall) - lie));

        /// <summary>The remains' final pose and the fall presentation shows on the way there.</summary>
        public static Felling Fell(in LocalTransform standing, in Topple topple, byte fall, bool witnessed,
            out LocalTransform pose)
        {
            pose = RemainsPose(standing, fall, topple.Lie);
            var trunk = math.rotate(standing.Rotation, topple.Trunk);
            return new Felling { Final = pose.Rotation, Tilt = FromTo(Direction(fall), trunk), Witnessed = witnessed };
        }

        /// <summary>The remains' rotation <paramref name="time"/> seconds into the fall.</summary>
        public static quaternion Pose(in Felling felling, float time) =>
            math.mul(math.slerp(felling.Tilt, quaternion.identity, Progress(time)), felling.Final);

        /// <summary>0 upright, 1 down: easing in as it tips, then a small damped bounce off the ground.</summary>
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

        /// <summary>The shortest turn taking unit <paramref name="from"/> to unit <paramref name="to"/>.</summary>
        private static quaternion FromTo(float3 from, float3 to)
        {
            var axis = math.cross(from, to);
            var length = math.length(axis);
            if (length < 1e-6f)
            {
                return quaternion.identity;
            }

            var angle = math.atan2(length, math.dot(from, to));
            return quaternion.AxisAngle(axis / length, angle);
        }
    }
}
