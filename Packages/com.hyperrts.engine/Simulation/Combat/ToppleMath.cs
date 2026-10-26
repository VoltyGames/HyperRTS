using HyperRTS.Simulation.Common;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.Combat
{
    /// <summary>Servers and clients both derive the remains pose from one fall heading byte, so footprints match.</summary>
    public static class ToppleMath
    {
        /// <summary>A hit from straight above gives heading 0.</summary>
        public static byte Quantize(float3 hitFrom, float3 position) => HeadingByte.Encode(position.xz - hitFrom.xz);

        public static LocalTransform RemainsPose(in LocalTransform standing, byte fall, float lie) =>
            standing.WithRotation(quaternion.RotateY(HeadingByte.Radians(fall) - lie));

        /// <summary>The remains' final pose and the fall presentation shows on the way.</summary>
        public static ToppleFall Fell(in LocalTransform standing, in Topple topple, byte fall, bool witnessed,
            out LocalTransform pose)
        {
            pose = RemainsPose(standing, fall, topple.Lie);
            var trunk = math.rotate(standing.Rotation, topple.Trunk);
            var canopy = HeadingByte.Direction(fall);
            return new ToppleFall
            {
                Tilt = Rotations.FromTo(canopy, trunk),
                CanopyDirection = canopy,
                Height = topple.Height * standing.Scale,
                Witnessed = witnessed,
            };
        }

        /// <summary>The remains' rotation <paramref name="progress"/> of the way from upright (0) to <paramref name="final"/> (1).</summary>
        public static quaternion Pose(in ToppleFall toppleFall, quaternion final, float progress) =>
            math.mul(math.slerp(toppleFall.Tilt, quaternion.identity, progress), final);
    }
}
