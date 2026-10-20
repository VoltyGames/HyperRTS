using HyperRTS.Simulation.Navigation;
using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Presentation.Animation
{
    /// <summary>
    /// A unit's client-side model motion: the tilt of its body and the frame state its <see cref="MotionRigPart"/>s
    /// need. Never moves the unit itself, only child transforms.
    /// </summary>
    public struct MotionRig : IComponentData
    {
        /// <summary>Seconds for the tilt to close most of the gap to its target.</summary>
        public const float TiltTime = 0.15f;

        /// <summary>Aircraft lean per m/s² of acceleration, and the most they lean.</summary>
        public const float LeanPerAcceleration = 3f * math.TORADIANS;
        public const float MaxLean = 20f * math.TORADIANS;

        /// <summary>The child that pitches and rolls, or null for none.</summary>
        public Entity TiltRoot;

        /// <summary>Rotation of the tilt root's parent relative to the unit.</summary>
        public quaternion TiltFrame;

        public quaternion TiltRest;

        /// <summary>Length (x along forward) and width the ground is sampled across.</summary>
        public float2 Footprint;

        /// <summary>Leans from acceleration instead of following the ground.</summary>
        public bool Aircraft;

        /// <summary>Frames seen, up to 2: velocity needs one before it, acceleration two.</summary>
        public byte Frames;

        public float3 LastPosition;
        public float3 LastVelocity;

        /// <summary>Current nose-up pitch and right-side-up roll, radians.</summary>
        public float2 Tilt;

        /// <summary>Rotation in the unit's frame that raises the nose by x and the right side by y radians.</summary>
        public static quaternion TiltRotation(float2 tilt) =>
            math.mul(quaternion.AxisAngle(math.forward(), tilt.y), quaternion.AxisAngle(math.right(), -tilt.x));

        /// <summary>Local rotation of the tilt root that shows <see cref="Tilt"/> in the unit's frame.</summary>
        public readonly quaternion TiltedRotation() =>
            math.mul(math.mul(math.inverse(TiltFrame), math.mul(TiltRotation(Tilt), TiltFrame)), TiltRest);

        /// <summary>Eases <see cref="Tilt"/> toward <paramref name="target"/>.</summary>
        public void SmoothTilt(float2 target, float deltaTime) =>
            Tilt = math.lerp(Tilt, target, 1f - math.exp(-deltaTime / TiltTime));

        /// <summary>Nose-up pitch and right-side-up roll of the ground under the footprint.</summary>
        public readonly float2 GroundTilt(in TerrainHeight terrain, float3 position, float3 forward, float3 right)
        {
            var half = Footprint * 0.5f;
            var front = terrain.Height((position + forward * half.x).xz);
            var back = terrain.Height((position - forward * half.x).xz);
            var rightSide = terrain.Height((position + right * half.y).xz);
            var leftSide = terrain.Height((position - right * half.y).xz);
            return new float2(math.atan2(front - back, Footprint.x), math.atan2(rightSide - leftSide, Footprint.y));
        }

        /// <summary>An aircraft noses down as it speeds up and banks into the turn.</summary>
        public static float2 LeanTilt(float3 acceleration, float3 forward, float3 right)
        {
            var lean = -new float2(math.dot(acceleration, forward), math.dot(acceleration, right)) * LeanPerAcceleration;
            return math.clamp(lean, -MaxLean, MaxLean);
        }
    }

    /// <summary>One posed child of a <see cref="MotionRig"/>, with its baked rest pose and per-frame state.</summary>
    [InternalBufferCapacity(0)]
    public struct MotionRigPart : IBufferElementData
    {
        public Entity Part;
        public MotionPartKind Kind;

        /// <summary>The <c>WeaponPose</c> index it follows (turret, elevation, recoil).</summary>
        public byte Pose;

        /// <summary>
        /// Local axis a positive angle turns about: up (turret), left (elevation), right (wheel), the rotor axis. A
        /// recoil part's local back, the way it kicks.
        /// </summary>
        public float3 Axis;

        /// <summary>Radians per second (turret, elevation, rotor at full speed).</summary>
        public float Rate;

        /// <summary>Wheel radius or recoil distance.</summary>
        public float Size;

        /// <summary>Elevation limits in radians.</summary>
        public float Min;
        public float Max;

        /// <summary>Recoil return or rotor spool seconds.</summary>
        public float Time;

        public quaternion RestRotation;
        public float3 RestPosition;

        /// <summary>Current angle in radians (turret, elevation, wheel, rotor).</summary>
        public float Angle;

        /// <summary>Recoil distance still pushed back, or rotor spool from 0 to 1.</summary>
        public float Amount;

        public byte LastShots;
        public bool Seen;

        public readonly quaternion Rotation => math.mul(RestRotation, quaternion.AxisAngle(Axis, Angle));

        /// <summary>Turns the shortest way toward <paramref name="yaw"/> at <see cref="Rate"/>.</summary>
        public void Traverse(float yaw, float deltaTime)
        {
            var gap = Wrap(yaw - Angle);
            Angle = Wrap(Angle + math.clamp(gap, -Rate * deltaTime, Rate * deltaTime));
        }

        /// <summary>Elevates toward <paramref name="pitch"/>, held within the limits, at <see cref="Rate"/>.</summary>
        public void Elevate(float pitch, float deltaTime)
        {
            var gap = math.clamp(pitch, Min, Max) - Angle;
            Angle += math.clamp(gap, -Rate * deltaTime, Rate * deltaTime);
        }

        /// <summary>Kicks back the full distance when the shot count changes, then eases back over <see cref="Time"/>.</summary>
        public void Recoil(byte shots, float deltaTime)
        {
            var fired = Seen && shots != LastShots;
            Amount = fired ? Size : math.max(0f, Amount - Size * deltaTime / Time);
            LastShots = shots;
            Seen = true;
        }

        public readonly float3 RecoilPosition(quaternion rotation) => RestPosition + math.rotate(rotation, Axis) * Amount;

        public void Roll(float distance) => Angle = Wrap(Angle + distance / Size);

        /// <summary>Spools toward full speed while running, down otherwise, and turns at the spooled speed.</summary>
        public void Spin(bool running, float deltaTime)
        {
            var target = running ? 1f : 0f;
            Amount += math.clamp(target - Amount, -deltaTime / Time, deltaTime / Time);
            Angle = Wrap(Angle + Rate * Amount * deltaTime);
        }

        private static float Wrap(float angle) => angle - 2f * math.PI * math.round(angle / (2f * math.PI));
    }

    /// <summary>How a <see cref="MotionRigPart"/> moves.</summary>
    public enum MotionPartKind : byte
    {
        Turret = 0,
        Elevation = 1,
        Recoil = 2,
        Wheel = 3,
        Rotor = 4,
    }
}
