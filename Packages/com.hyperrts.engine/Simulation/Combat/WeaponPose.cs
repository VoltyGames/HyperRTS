using HyperRTS.Simulation.Common;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;

namespace HyperRTS.Simulation.Combat
{
    /// <summary>
    /// Where one weapon points and how often it fired, replicated so clients can turn turrets and play recoil. The
    /// primary is index 0 and mount i is at <see cref="WeaponMount.Pose"/>; the buffer keeps its baked length.
    /// </summary>
    [InternalBufferCapacity(1)]
    public struct WeaponPose : IBufferElementData
    {
        /// <summary>Signed 0.75 degree steps reach -96 to 95 degrees, enough for the clamp below.</summary>
        private const float PitchStep = 0.75f * math.TORADIANS;

        private const float MinPitch = -30f * math.TORADIANS;
        private const float MaxPitch = 80f * math.TORADIANS;

        /// <summary>Heading relative to the hull in <see cref="HeadingByte"/> steps; 0 points straight ahead.</summary>
        [GhostField] public byte Yaw;

        /// <summary>Elevation as a signed byte of <see cref="PitchStep"/>s, so the default pose is level.</summary>
        [GhostField] public byte Pitch;

        /// <summary>Wraps; clients play a shot each time it changes.</summary>
        [GhostField] public byte Shots;

        public float YawRadians => (sbyte)Yaw * (2f * math.PI / HeadingByte.Steps);

        public float PitchRadians => (sbyte)Pitch * PitchStep;

        /// <summary>Points at <paramref name="localDirection"/> (in the hull's space) at <paramref name="elevation"/> radians.</summary>
        public void Aim(float3 localDirection, float elevation)
        {
            Yaw = HeadingByte.Encode(localDirection.xz);
            Pitch = (byte)(sbyte)math.round(math.clamp(elevation, MinPitch, MaxPitch) / PitchStep);
        }

        /// <summary>Eases back to straight ahead and level by at most <paramref name="steps"/> per axis.</summary>
        public void Settle(int steps)
        {
            Yaw = Toward((sbyte)Yaw, steps);
            Pitch = Toward((sbyte)Pitch, steps);
        }

        public void CountShot() => Shots = (byte)(Shots + 1);

        private static byte Toward(int value, int steps) => (byte)(sbyte)(value - math.clamp(value, -steps, steps));
    }
}
