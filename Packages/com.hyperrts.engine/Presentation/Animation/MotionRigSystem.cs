using HyperRTS.Simulation.Air;
using HyperRTS.Simulation.Combat;
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
    /// replicated <see cref="WeaponPose"/>, barrels recoil per shot, wheels roll with the ground covered, rotors spool
    /// with flight and the body tilts with the ground or leans with acceleration.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
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
                Transforms = SystemAPI.GetComponentLookup<LocalTransform>(),
            }.Schedule(state.Dependency);
        }

        [BurstCompile]
        private partial struct RigJob : IJobEntity
        {
            public float DeltaTime;
            public TerrainHeight Terrain;
            [ReadOnly] public BufferLookup<WeaponPose> Poses;
            [ReadOnly] public ComponentLookup<Flight> Flights;
            [ReadOnly] public ComponentLookup<Docked> Docks;
            public ComponentLookup<LocalTransform> Transforms;

            private void Execute(Entity entity, ref MotionRig rig, DynamicBuffer<MotionRigPart> parts, in LocalToWorld place)
            {
                var forward = math.normalizesafe(new float3(place.Forward.x, 0f, place.Forward.z), math.forward());
                var right = math.cross(math.up(), forward);
                var moved = rig.Frames > 0 ? place.Position - rig.LastPosition : float3.zero;
                var velocity = moved / DeltaTime;
                var acceleration = rig.Frames > 1 ? (velocity - rig.LastVelocity) / DeltaTime : float3.zero;
                rig.Frames = (byte)math.min(rig.Frames + 1, 2);
                rig.LastPosition = place.Position;
                rig.LastVelocity = velocity;

                Tilt(ref rig, place.Position, forward, right, acceleration);
                var rolled = math.length(moved.xz) * math.sign(math.dot(moved, forward));
                var docked = Docks.HasComponent(entity) && Docks.IsComponentEnabled(entity);
                var running = Flights.HasComponent(entity) && !docked;
                for (var i = 0; i < parts.Length; i++)
                {
                    var part = parts[i];
                    var transform = Transforms[part.Part];
                    Pose(ref part, ref transform, entity, rolled, running);
                    parts[i] = part;
                    Transforms[part.Part] = transform;
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
                var transform = Transforms[rig.TiltRoot];
                transform.Rotation = rig.TiltedRotation();
                Transforms[rig.TiltRoot] = transform;
            }

            private void Pose(ref MotionRigPart part, ref LocalTransform transform, Entity entity, float rolled, bool running)
            {
                switch (part.Kind)
                {
                    case MotionPartKind.Turret:
                        part.Traverse(Poses[entity][part.Pose].YawRadians, DeltaTime);
                        transform.Rotation = part.Rotation;
                        break;
                    case MotionPartKind.Elevation:
                        part.Elevate(Poses[entity][part.Pose].PitchRadians, DeltaTime);
                        transform.Rotation = part.Rotation;
                        break;
                    case MotionPartKind.Recoil:
                        part.Recoil(Poses[entity][part.Pose].Shots, DeltaTime);
                        transform.Position = part.RecoilPosition(transform.Rotation);
                        break;
                    case MotionPartKind.Wheel:
                        part.Roll(rolled);
                        transform.Rotation = part.Rotation;
                        break;
                    default:
                        part.Spin(running, DeltaTime);
                        transform.Rotation = part.Rotation;
                        break;
                }
            }
        }
    }
}
