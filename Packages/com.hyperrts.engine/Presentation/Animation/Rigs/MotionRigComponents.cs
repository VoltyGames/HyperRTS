using HyperRTS.Simulation.Common.Geometry;
using HyperRTS.Simulation.Death;
using HyperRTS.Simulation.Navigation.Heights;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Presentation.Animation.Rigs
{
    /// <summary>
    /// A unit's client-side model motion: the tilt of its body and the frame state its <see cref="MotionRigPart"/>s
    /// need. Never moves the unit itself, only child transforms.
    /// </summary>
    public struct MotionRig : IComponentData
    {
        /// <summary>Seconds for the tilt to close most of the gap to its target.</summary>
        public const float TiltTime = 0.15f;

        /// <summary>Seconds for an aircraft's lean to close most of the gap, slower than a hull on its springs.</summary>
        public const float LeanTime = 0.5f;

        /// <summary>Nose-down pitch an aircraft holds at its cruise speed, and the most it leans.</summary>
        public const float CruisePitch = 7f * math.TORADIANS;
        public const float MaxLean = 30f * math.TORADIANS;

        /// <summary>The child that pitches and rolls, or null for none.</summary>
        public Entity TiltRoot;

        /// <summary>The same body in another stage's model, posed like <see cref="TiltRoot"/>, or null.</summary>
        public Entity TiltFollower;

        /// <summary>Rotation of the tilt root's parent relative to the unit.</summary>
        public quaternion TiltFrame;

        public quaternion TiltRest;

        /// <summary>Length (x along forward) and width the ground is sampled across.</summary>
        public float2 Footprint;

        /// <summary>Leans from acceleration instead of following the ground; baked from the unit's Flight.</summary>
        public bool Aircraft;

        /// <summary>Frames seen, up to 2: velocity needs one before it, acceleration two.</summary>
        public byte Frames;

        public float3 LastPosition;
        public float3 LastVelocity;

        /// <summary>Forward on XZ last frame, to measure the turn.</summary>
        public float2 LastHeading;

        /// <summary>Turn per metre rolled in 1/m, positive to the left, which steered wheels follow.</summary>
        public float Curvature;

        /// <summary>Current nose-up pitch and right-side-up roll from the ground or the lean, radians.</summary>
        public float2 Tilt;

        /// <summary>Ground vehicles sway on this over their <see cref="Tilt"/>.</summary>
        public Suspension Suspension;

        /// <summary>Rotation in the unit's frame that raises the nose by x and the right side by y radians.</summary>
        public static quaternion TiltRotation(float2 tilt) =>
            math.mul(quaternion.AxisAngle(math.forward(), tilt.y), quaternion.AxisAngle(math.right(), -tilt.x));

        /// <summary>Local rotation of the tilt root that shows <see cref="Tilt"/> and the sway in the unit's frame.</summary>
        public readonly quaternion TiltedRotation() =>
            math.mul(math.mul(math.inverse(TiltFrame), math.mul(BodyTilt(), TiltFrame)), TiltRest);

        /// <summary>The body's turn from <see cref="Tilt"/> and the sway, in the unit's frame.</summary>
        public readonly quaternion BodyTilt() => TiltRotation(Tilt + Suspension.Sway);

        /// <summary>Eases <see cref="Tilt"/> toward <paramref name="target"/>.</summary>
        public void SmoothTilt(float2 target, float deltaTime) =>
            Tilt = math.lerp(Tilt, target, Ease(deltaTime, TiltTime));

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

        /// <summary>
        /// Metres the ground under a wheel's <paramref name="contact"/> (its rest point on the ground plane, in the
        /// unit's frame) stands above where that point of the body turned by <paramref name="bodyTilt"/> is.
        /// </summary>
        public static float GroundLift(in TerrainHeight terrain, float3 position, float3 forward, float3 right,
            quaternion bodyTilt, float3 contact)
        {
            var body = math.rotate(bodyTilt, contact);
            var ground = position + right * body.x + forward * body.z;
            return terrain.Height(ground.xz) - (position.y + body.y);
        }

        /// <summary>
        /// Measures <see cref="Curvature"/> from the turn since last frame over the metres rolled; standing keeps it.
        /// </summary>
        public void MeasureCurvature(float2 heading, float rolled)
        {
            if (Frames > 0 && math.abs(rolled) > MinRoll)
            {
                var turn = Planar.SignedAngle(LastHeading, heading);
                Curvature = turn / rolled;
            }

            LastHeading = heading;
        }

        // Below this the turn over the distance is noise.
        private const float MinRoll = 0.01f;

        /// <summary>Share of the gap an ease over <paramref name="time"/> seconds closes in one frame.</summary>
        internal static float Ease(float deltaTime, float time) => 1f - math.exp(-deltaTime / time);

        /// <summary>
        /// Eases an aircraft's <see cref="Tilt"/> toward its lean: nose down with forward speed and as it speeds up,
        /// nose up as it brakes, banked into the turn as its acceleration tips the lift. It holds
        /// <see cref="CruisePitch"/> at <paramref name="topSpeed"/>.
        /// </summary>
        public void Lean(float3 velocity, float3 acceleration, float3 forward, float3 right, float topSpeed,
            float deltaTime)
        {
            var cruise = math.saturate(math.dot(velocity, forward) / math.max(topSpeed, 1e-3f));
            var pitch = -math.atan(math.dot(acceleration, forward) / FallingRemains.Gravity)
                - CruisePitch * cruise * cruise;
            var roll = -math.atan(math.dot(acceleration, right) / FallingRemains.Gravity);
            // Clamped after easing, so a one-frame spike of measured acceleration counts for its share, not more.
            var eased = math.lerp(Tilt, new float2(pitch, roll), Ease(deltaTime, LeanTime));
            Tilt = math.clamp(eased, -MaxLean, MaxLean);
        }
    }

    /// <summary>One moving child of a <see cref="MotionRig"/>, with its baked rest pose and per-frame state.</summary>
    [InternalBufferCapacity(0)]
    public struct MotionRigPart : IBufferElementData
    {
        /// <summary>Seconds for a turret or barrel to close most of the gap to the replicated pose.</summary>
        public const float FollowTime = 0.08f;

        /// <summary>Seconds for a wheel to close most of the gap to the ground.</summary>
        public const float LiftTime = 0.06f;

        public Entity Part;
        public MotionPartKind Kind;

        /// <summary>One more than the index of the next entry on the same <see cref="Part"/>, 0 for none.</summary>
        public int Following;

        /// <summary>The <c>WeaponPose</c> index it follows (turret, elevation, recoil).</summary>
        public byte Weapon;

        /// <summary>
        /// Local axis a positive angle turns about: up (turret), left (elevation), right (wheel), the rotor axis. A
        /// recoil part's local back, the way it kicks.
        /// </summary>
        public float3 Axis;

        /// <summary>Rotor radians per second at full speed, or a trailer's largest angle to the hull in radians.</summary>
        public float Rate;

        /// <summary>Wheel radius, recoil distance, or a trailer's metres from hitch to axles.</summary>
        public float Size;

        /// <summary>Local axis a steering wheel turns about, down so a positive angle steers left.</summary>
        public float3 SteerAxis;

        /// <summary>Metres a steering wheel sits ahead of the rear axle, which sets how far it turns.</summary>
        public float RearAxleDistance;

        /// <summary>Recoil return or rotor spin-up seconds.</summary>
        public float Time;

        /// <summary>Metres a wheel may rise or sink from its rest to meet the ground; 0 for parts that stay put.</summary>
        public float Travel;

        /// <summary>Where a ground-following wheel rests on the ground plane, in the unit's frame.</summary>
        public float3 Contact;

        /// <summary>The parent's local up, which a ground-following wheel lifts along.</summary>
        public float3 LiftAxis;

        /// <summary>Metres a ground-following wheel is lifted from its rest.</summary>
        public float Lift;

        public quaternion RestRotation;
        public float3 RestPosition;

        /// <summary>Current angle in radians (turret, elevation, wheel, rotor, a trailer's to the hull).</summary>
        public float Angle;

        /// <summary>Recoil distance still pushed back, rotor speed from 0 to 1, or a steering wheel's angle.</summary>
        public float Amount;

        public ChangeTimer Shots;

        public readonly quaternion Rotation => Kind == MotionPartKind.SteeringWheel
            ? math.mul(RestRotation, math.mul(quaternion.AxisAngle(SteerAxis, Amount), quaternion.AxisAngle(Axis, Angle)))
            : math.mul(RestRotation, quaternion.AxisAngle(Axis, Angle));

        /// <summary>Eases the shortest way toward the simulated <paramref name="yaw"/>, which already turns at the weapon's rate.</summary>
        public void Traverse(float yaw, float deltaTime) =>
            Angle = HeadingByte.Wrap(Angle + HeadingByte.Wrap(yaw - Angle) * MotionRig.Ease(deltaTime, FollowTime));

        /// <summary>Eases toward the simulated <paramref name="pitch"/>, which already keeps the weapon's limits.</summary>
        public void Elevate(float pitch, float deltaTime) => Angle += (pitch - Angle) * MotionRig.Ease(deltaTime, FollowTime);

        /// <summary>Kicks back the full distance when the shot count changes, then eases back over <see cref="Time"/>. True on a shot.</summary>
        public bool Recoil(byte shots, float deltaTime)
        {
            var fired = Shots.Update(shots, deltaTime);
            Amount = fired ? Size : math.max(0f, Amount - Size * deltaTime / Time);
            return fired;
        }

        /// <summary>Puts this frame's rotation, recoil and lift on the part's transform.</summary>
        public readonly void ApplyTo(ref LocalTransform transform)
        {
            if (Kind == MotionPartKind.Recoil)
            {
                transform.Position = RecoilPosition(transform.Rotation);
            }
            else
            {
                transform.Rotation = Rotation;
            }

            if (Travel > 0f)
            {
                transform.Position = LiftedPosition;
            }
        }

        public readonly float3 RecoilPosition(quaternion rotation) => RestPosition + math.rotate(rotation, Axis) * Amount;

        public readonly float3 LiftedPosition => RestPosition + LiftAxis * Lift;

        /// <summary>Eases the lift toward <paramref name="wanted"/> metres, held to the wheel's travel.</summary>
        public void FollowGround(float wanted, float deltaTime) =>
            Lift += (math.clamp(wanted, -Travel, Travel) - Lift) * MotionRig.Ease(deltaTime, LiftTime);

        public void Roll(float distance) => Angle = HeadingByte.Wrap(Angle + distance / Size);

        /// <summary>Eases a steering wheel toward the angle that rolls it round the rear axle's arc.</summary>
        public void Steer(float curvature, float deltaTime) =>
            Amount += (math.atan(RearAxleDistance * curvature) - Amount) * MotionRig.Ease(deltaTime, FollowTime);

        /// <summary>
        /// Swings a trailer after the hull, as a towed trailer on a hitch over the rear axle does: its angle to the hull
        /// closes as it rolls and opens as the hull turns. Either way it closes, so backing never jackknifes it.
        /// </summary>
        public void Tow(float rolled, float curvature) =>
            Angle = math.clamp(Angle - math.abs(rolled) / Size * math.sin(Angle) - curvature * rolled, -Rate, Rate);

        /// <summary>Spins up toward full speed while running, down otherwise, and turns at that speed.</summary>
        public void Spin(bool running, float deltaTime)
        {
            var target = running ? 1f : 0f;
            Amount += math.clamp(target - Amount, -deltaTime / Time, deltaTime / Time);
            Angle = HeadingByte.Wrap(Angle + Rate * Amount * deltaTime);
        }

    }

    /// <summary>
    /// How fast a rig's rotors turn, 0 still to 1 full, written by <c>MotionRigSystem</c> so the rotor sound follows
    /// the blades. Only rigs with a rotor have it.
    /// </summary>
    public struct RotorSpin : IComponentData
    {
        public float Amount;
    }

    /// <summary>How a <see cref="MotionRigPart"/> moves.</summary>
    public enum MotionPartKind : byte
    {
        Turret = 0,
        Elevation = 1,
        Recoil = 2,
        Wheel = 3,
        Rotor = 4,
        SteeringWheel = 5,
        Trailer = 6,
    }
}
