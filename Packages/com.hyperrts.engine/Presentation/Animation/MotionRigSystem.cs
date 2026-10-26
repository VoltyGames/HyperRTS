using HyperRTS.Simulation.Air;
using HyperRTS.Simulation.Combat;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Navigation;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Presentation.Animation
{
    /// <summary>
    /// Poses each <see cref="MotionRig"/>'s children from the interpolated unit: turrets and barrels follow the
    /// replicated <see cref="WeaponPose"/>, barrels recoil per shot, wheels roll with the ground covered,
    /// rotors spin up with flight into the unit's <see cref="RotorSpin"/>, and the body tilts with the ground or leans with
    /// acceleration. Ground vehicles also sway on their <see cref="Suspension"/>.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(PoseSystemGroup))]
    public partial struct MotionRigSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state) => state.RequireForUpdate<MotionRig>();

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var deltaTime = SystemAPI.Time.DeltaTime;
            // Paused: nothing moved, and the rates below divide by the step.
            if (deltaTime <= 0f)
            {
                return;
            }

            SystemAPI.TryGetSingleton<TerrainHeight>(out var terrain);
            state.Dependency = new RigJob
            {
                DeltaTime = deltaTime,
                Terrain = terrain,
                Poses = SystemAPI.GetBufferLookup<WeaponPose>(true),
                Flights = SystemAPI.GetComponentLookup<Flight>(true),
                Docks = SystemAPI.GetComponentLookup<Docked>(true),
                Spins = SystemAPI.GetComponentLookup<RotorSpin>(),
                Transforms = SystemAPI.GetComponentLookup<LocalTransform>(),
            }.Schedule(state.Dependency);
        }

        [BurstCompile]
        [WithAll(typeof(LocalTransform))]
        private partial struct RigJob : IJobEntity
        {
            public float DeltaTime;
            public TerrainHeight Terrain;
            [ReadOnly] public BufferLookup<WeaponPose> Poses;
            [ReadOnly] public ComponentLookup<Flight> Flights;
            [ReadOnly] public ComponentLookup<Docked> Docks;
            public ComponentLookup<RotorSpin> Spins;
            public ComponentLookup<LocalTransform> Transforms;

            // Roots are top-level, so LocalTransform is this frame's pose; read by lookup, since a parameter would alias it.
            private void Execute(Entity entity, ref MotionRig rig, DynamicBuffer<MotionRigPart> parts)
            {
                var place = Transforms[entity];
                var facing = place.Forward();
                var forward = math.normalizesafe(new float3(facing.x, 0f, facing.z), math.forward());
                var right = math.cross(math.up(), forward);
                var moved = rig.Frames > 0 ? place.Position - rig.LastPosition : float3.zero;
                var velocity = moved / DeltaTime;
                var acceleration = rig.Frames > 1 ? (velocity - rig.LastVelocity) / DeltaTime : float3.zero;
                rig.Frames = (byte)math.min(rig.Frames + 1, 2);
                rig.LastPosition = place.Position;
                rig.LastVelocity = velocity;

                Tilt(ref rig, place.Position, forward, right, acceleration);
                var rolled = math.length(moved.xz) * math.sign(math.dot(moved, forward));
                var running = Flights.HasComponent(entity) && !Docks.HasEnabled(entity);
                Poses.TryGetBuffer(entity, out var poses);
                var spin = 0f;
                for (var i = 0; i < parts.Length; i++)
                {
                    var part = parts[i];
                    var transform = Transforms[part.Part];
                    if (Pose(ref part, ref transform, poses, rolled, running))
                    {
                        rig.Suspension.Recoil(poses[part.Weapon].YawRadians);
                    }

                    parts[i] = part;
                    Transforms[part.Part] = transform;
                    spin = part.Kind == MotionPartKind.Rotor ? math.max(spin, part.Amount) : spin;
                }

                if (Spins.HasComponent(entity))
                {
                    Spins[entity] = new RotorSpin { Amount = spin };
                }
            }

            private void Tilt(ref MotionRig rig, float3 position, float3 forward, float3 right, float3 acceleration)
            {
                if (rig.TiltRoot == Entity.Null)
                {
                    return;
                }

                var target = rig.Aircraft
                    ? MotionRig.LeanTilt(acceleration, forward, right)
                    : rig.GroundTilt(Terrain, position, forward, right);
                rig.SmoothTilt(target, DeltaTime);
                rig.Suspension.Spring(rig.Suspension.Target(acceleration, forward, right), DeltaTime);

                var transform = Transforms[rig.TiltRoot];
                transform.Rotation = rig.TiltedRotation();
                Transforms[rig.TiltRoot] = transform;
                if (rig.TiltFollower != Entity.Null)
                {
                    var stage = Transforms[rig.TiltFollower];
                    stage.Rotation = transform.Rotation;
                    Transforms[rig.TiltFollower] = stage;
                }
            }

            /// <summary>True when a recoil part fired this frame.</summary>
            private bool Pose(ref MotionRigPart part, ref LocalTransform transform, DynamicBuffer<WeaponPose> poses,
                float rolled, bool running)
            {
                // Recoil slides along the barrel's current rotation, which an elevation part may own.
                if (part.Kind == MotionPartKind.Recoil)
                {
                    var fired = part.Recoil(poses[part.Weapon].Shots, DeltaTime);
                    transform.Position = part.RecoilPosition(transform.Rotation);
                    return fired;
                }

                switch (part.Kind)
                {
                    case MotionPartKind.Turret:
                        part.Traverse(poses[part.Weapon].YawRadians, DeltaTime);
                        break;
                    case MotionPartKind.Elevation:
                        part.Elevate(poses[part.Weapon].PitchRadians, DeltaTime);
                        break;
                    case MotionPartKind.Wheel:
                        part.Roll(rolled);
                        break;
                    default:
                        part.Spin(running, DeltaTime);
                        break;
                }

                transform.Rotation = part.Rotation;
                return false;
            }
        }
    }
}
