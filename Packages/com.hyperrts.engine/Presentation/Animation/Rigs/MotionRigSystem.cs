using HyperRTS.Simulation.Air;
using HyperRTS.Simulation.Combat.Weapons;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Navigation.Heights;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Presentation.Animation.Rigs
{
    /// <summary>
    /// Poses each <see cref="MotionRig"/>'s children from the interpolated unit: turrets, barrels, wheels, rotors and
    /// body tilt. Ground vehicles also sway on their <see cref="Suspension"/>.
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
            var rigs = new RigJob
            {
                DeltaTime = deltaTime,
                Terrain = terrain,
                Poses = SystemAPI.GetBufferLookup<WeaponPose>(true),
                Flights = SystemAPI.GetComponentLookup<Flight>(true),
                Docks = SystemAPI.GetComponentLookup<Docked>(true),
            }.ScheduleParallel(state.Dependency);

            // Parts and track meshes pull their pose from their rig, so every job writes only the entity it runs on.
            var spins = new SpinJob().ScheduleParallel(rigs);
            var parts = new PartJob
            {
                Rigs = SystemAPI.GetComponentLookup<MotionRig>(true),
                Parts = SystemAPI.GetBufferLookup<MotionRigPart>(true),
            }.ScheduleParallel(rigs);
            var tracks = new TrackJob().ScheduleParallel(rigs);
            var meshes = new TrackMeshJob
            {
                Tracks = SystemAPI.GetBufferLookup<MotionRigTrack>(true),
                Sources = SystemAPI.GetComponentTypeHandle<TrackMeshSource>(true),
                Fronts = SystemAPI.GetComponentTypeHandle<TrackLift0>(),
                Backs = SystemAPI.GetComponentTypeHandle<TrackLift1>(),
            }.ScheduleParallel(SystemAPI.QueryBuilder().WithAll<TrackMeshSource>().WithAllRW<TrackLift0, TrackLift1>()
                .Build(), tracks);
            state.Dependency = JobHandle.CombineDependencies(spins, parts, meshes);
        }

        [BurstCompile]
        private partial struct RigJob : IJobEntity
        {
            public float DeltaTime;
            public TerrainHeight Terrain;
            [ReadOnly] public BufferLookup<WeaponPose> Poses;
            [ReadOnly] public ComponentLookup<Flight> Flights;
            [ReadOnly] public ComponentLookup<Docked> Docks;

            // Roots are top-level, so LocalTransform is this frame's pose.
            private void Execute(Entity entity, ref MotionRig rig, DynamicBuffer<MotionRigPart> parts, in LocalTransform place)
            {
                var heading = new RigHeading(place);
                var moved = rig.Frames > 0 ? place.Position - rig.LastPosition : float3.zero;
                var velocity = moved / DeltaTime;
                var acceleration = rig.Frames > 1 ? (velocity - rig.LastVelocity) / DeltaTime : float3.zero;
                var rolled = math.length(moved.xz) * math.sign(math.dot(moved, heading.Forward));
                rig.MeasureCurvature(heading.Forward.xz, rolled);
                rig.Frames = (byte)math.min(rig.Frames + 1, 2);
                rig.LastPosition = place.Position;
                rig.LastVelocity = velocity;

                Tilt(ref rig, heading, acceleration);
                AdvanceParts(entity, ref rig, parts, heading, rolled);
            }

            private void AdvanceParts(Entity entity, ref MotionRig rig, DynamicBuffer<MotionRigPart> parts,
                in RigHeading heading, float rolled)
            {
                var running = Flights.HasComponent(entity) && !Docks.HasEnabled(entity);
                Poses.TryGetBuffer(entity, out var poses);
                var bodyTilt = rig.BodyTilt();
                for (var i = 0; i < parts.Length; i++)
                {
                    var part = parts[i];
                    if (Advance(ref part, poses, rolled, rig.Curvature, running))
                    {
                        rig.Suspension.Recoil(poses[part.Weapon].YawRadians);
                    }

                    if (part.Travel > 0f)
                    {
                        var lift = MotionRig.GroundLift(Terrain, heading.Position, heading.Forward, heading.Right,
                            bodyTilt, part.Contact);
                        part.FollowGround(lift, DeltaTime);
                    }

                    parts[i] = part;
                }
            }

            private void Tilt(ref MotionRig rig, in RigHeading heading, float3 acceleration)
            {
                if (rig.TiltRoot == Entity.Null)
                {
                    return;
                }

                var target = rig.Aircraft
                    ? MotionRig.LeanTilt(acceleration, heading.Forward, heading.Right)
                    : rig.GroundTilt(Terrain, heading.Position, heading.Forward, heading.Right);
                rig.SmoothTilt(target, DeltaTime);
                rig.Suspension.Spring(rig.Suspension.Target(acceleration, heading.Forward, heading.Right), DeltaTime);
            }

            /// <summary>True when a recoil part fired this frame.</summary>
            private bool Advance(ref MotionRigPart part, DynamicBuffer<WeaponPose> poses, float rolled, float curvature,
                bool running)
            {
                switch (part.Kind)
                {
                    case MotionPartKind.Recoil:
                        return part.Recoil(poses[part.Weapon].Shots, DeltaTime);
                    case MotionPartKind.Turret:
                        part.Traverse(poses[part.Weapon].YawRadians, DeltaTime);
                        break;
                    case MotionPartKind.Elevation:
                        part.Elevate(poses[part.Weapon].PitchRadians, DeltaTime);
                        break;
                    case MotionPartKind.Wheel:
                        part.Roll(rolled);
                        break;
                    case MotionPartKind.SteeringWheel:
                        part.Roll(rolled);
                        part.Steer(curvature, DeltaTime);
                        break;
                    case MotionPartKind.Trailer:
                        part.Tow(rolled, curvature);
                        break;
                    default:
                        part.Spin(running, DeltaTime);
                        break;
                }

                return false;
            }
        }

        [BurstCompile]
        private partial struct SpinJob : IJobEntity
        {
            private void Execute(ref RotorSpin spin, in DynamicBuffer<MotionRigPart> parts)
            {
                var amount = 0f;
                for (var i = 0; i < parts.Length; i++)
                {
                    var part = parts[i];
                    amount = part.Kind == MotionPartKind.Rotor ? math.max(amount, part.Amount) : amount;
                }

                spin.Amount = amount;
            }
        }

        [BurstCompile]
        private partial struct TrackJob : IJobEntity
        {
            private void Execute(DynamicBuffer<MotionRigTrack> tracks, DynamicBuffer<TrackWheel> wheels,
                in DynamicBuffer<MotionRigPart> parts)
            {
                for (var i = 0; i < wheels.Length; i++)
                {
                    var wheel = wheels[i];
                    wheel.Lift = parts[wheel.Part].Lift;
                    wheels[i] = wheel;
                }

                for (var i = 0; i < tracks.Length; i++)
                {
                    var track = tracks[i];
                    track.Settle(wheels.AsNativeArray().GetSubArray(track.FirstWheel, track.WheelCount));
                    tracks[i] = track;
                }
            }
        }

        [BurstCompile]
        private struct TrackMeshJob : IJobChunk
        {
            [ReadOnly] public BufferLookup<MotionRigTrack> Tracks;
            [ReadOnly] public ComponentTypeHandle<TrackMeshSource> Sources;
            public ComponentTypeHandle<TrackLift0> Fronts;
            public ComponentTypeHandle<TrackLift1> Backs;

            // Takes the writable arrays only for a chunk with a moved track, so settled tracks keep their version
            // and skip the re-upload.
            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask,
                in v128 chunkEnabledMask)
            {
                var sources = chunk.GetNativeArray(ref Sources);
                var moved = false;
                for (var i = 0; i < chunk.Count && !moved; i++)
                {
                    moved = Tracks[sources[i].Rig][sources[i].Track].Moved;
                }

                if (!moved)
                {
                    return;
                }

                var fronts = chunk.GetNativeArray(ref Fronts);
                var backs = chunk.GetNativeArray(ref Backs);
                for (var i = 0; i < chunk.Count; i++)
                {
                    var track = Tracks[sources[i].Rig][sources[i].Track];
                    fronts[i] = new TrackLift0 { Value = track.Lift0 };
                    backs[i] = new TrackLift1 { Value = track.Lift1 };
                }
            }
        }

        [BurstCompile]
        private partial struct PartJob : IJobEntity
        {
            [ReadOnly] public ComponentLookup<MotionRig> Rigs;
            [ReadOnly] public BufferLookup<MotionRigPart> Parts;

            private void Execute(Entity entity, ref LocalTransform transform, in MotionRigLink link)
            {
                ref readonly var rig = ref Rigs.GetRefRO(link.Rig).ValueRO;
                if (rig.TiltRoot == entity || rig.TiltFollower == entity)
                {
                    transform.Rotation = rig.TiltedRotation();
                }

                // Entries on one entity apply in order, so a recoil reads the elevation before it.
                var parts = Parts[link.Rig];
                for (var i = link.FirstPart; i >= 0; i = parts[i].Following - 1)
                {
                    parts[i].ApplyTo(ref transform);
                }
            }
        }
    }
}
