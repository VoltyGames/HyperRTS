using HyperRTS.Simulation.Common;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Netcode;

namespace HyperRTS.Simulation.Combat
{
    /// <summary>
    /// Where one weapon points and how often it fired, replicated so clients can turn turrets and play recoil. The
    /// primary is index 0 and mount i is at <see cref="WeaponMount.Pose"/>; the buffer keeps its baked length.
    /// </summary>
    [InternalBufferCapacity(1)]
    public struct WeaponPose : IBufferElementData
    {
        /// <summary>Signed 0.75 degree steps reach -96 to 95 degrees.</summary>
        private const float PitchStep = 0.75f * math.TORADIANS;

        private const float YawStep = 2f * math.PI / HeadingByte.Steps;

        /// <summary>Heading relative to the hull in <see cref="HeadingByte"/> steps; 0 points straight ahead.</summary>
        [GhostField] public byte Yaw;

        /// <summary>Elevation as a signed byte of <see cref="PitchStep"/>s, so the default pose is level.</summary>
        [GhostField] public byte Pitch;

        /// <summary>Wraps; clients play a shot each time it changes.</summary>
        [GhostField] public byte Shots;

        /// <summary>The server's exact yaw and pitch in radians, which the bytes round for clients.</summary>
        [GhostField(SendData = false)] public float YawAngle;
        [GhostField(SendData = false)] public float PitchAngle;

        public float YawRadians => (sbyte)Yaw * YawStep;

        public float PitchRadians => (sbyte)Pitch * PitchStep;

        /// <summary>Turns the shortest way toward <paramref name="angles"/> by at most <paramref name="steps"/> radians per axis.</summary>
        public void Turn(float2 angles, float2 steps)
        {
            var gap = new float2(Wrap(angles.x - YawAngle), angles.y - PitchAngle);
            YawAngle = Wrap(YawAngle + math.clamp(gap.x, -steps.x, steps.x));
            PitchAngle += math.clamp(gap.y, -steps.y, steps.y);
            Yaw = (byte)((int)math.round(YawAngle / YawStep) & (HeadingByte.Steps - 1));
            Pitch = (byte)(sbyte)math.round(PitchAngle / PitchStep);
        }

        /// <summary>Radians between this pose and <paramref name="angles"/> on each axis.</summary>
        public readonly float2 Gap(float2 angles) =>
            math.abs(new float2(Wrap(angles.x - YawAngle), angles.y - PitchAngle));

        public void CountShot() => Shots = (byte)(Shots + 1);

        private static float Wrap(float angle) => angle - 2f * math.PI * math.round(angle / (2f * math.PI));
    }
}
