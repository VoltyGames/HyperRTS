using HyperRTS.Presentation.Environment;
using HyperRTS.Simulation.Animation;
using HyperRTS.Simulation.Combat;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Production;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Presentation.Animation
{
    /// <summary>
    /// Runs machine clocks, easing them to a stop while a machine shouldn't run (neutral, or its building offline or
    /// idle), and poses their parts: spins, swings and slides in step with the spin, links that sit on one point
    /// aiming at another, parts that turn downwind, parts that raise or extend over their span of the speed, which a
    /// Deployed machine takes from its unit's deploy progress, and parts that point at another. Links and pointers
    /// read last frame's transforms.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(PoseSystemGroup))]
    public partial struct MachineSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state) => state.RequireForUpdate<MachineClock>();

        // Not Burst: it reads the managed wind.
        public void OnUpdate(ref SystemState state)
        {
            state.Dependency = new ClockJob
            {
                DeltaTime = SystemAPI.Time.DeltaTime,
                Factions = SystemAPI.GetComponentLookup<Faction>(true),
                Offlines = SystemAPI.GetComponentLookup<Offline>(true),
                Queues = SystemAPI.GetBufferLookup<ProductionQueueItem>(true),
                Poses = SystemAPI.GetBufferLookup<WeaponPose>(true),
                Deployables = SystemAPI.GetComponentLookup<Deployable>(true),
            }.ScheduleParallel(state.Dependency);
            state.Dependency = new PartJob
            {
                Clocks = SystemAPI.GetComponentLookup<MachineClock>(true),
                Places = SystemAPI.GetComponentLookup<LocalToWorld>(true),
                Downwind = (float3)Wind.Downwind,
            }.ScheduleParallel(state.Dependency);
        }

        [BurstCompile]
        private partial struct ClockJob : IJobEntity
        {
            public float DeltaTime;
            [ReadOnly] public ComponentLookup<Faction> Factions;
            [ReadOnly] public ComponentLookup<Offline> Offlines;
            [ReadOnly] public BufferLookup<ProductionQueueItem> Queues;
            [ReadOnly] public BufferLookup<WeaponPose> Poses;
            [ReadOnly] public ComponentLookup<Deployable> Deployables;

            private void Execute(Entity entity, ref MachineClock clock)
            {
                if (clock.Run == MachineRun.Deployed)
                {
                    clock.Follow(Deployables[clock.Owner].Progress, DeltaTime);
                    return;
                }

                var owned = Factions.TryGetComponent(entity, out var faction) && faction.Value != Faction.Neutral;
                clock.Advance((!clock.OwnedOnly || owned) && Runs(ref clock), DeltaTime);
            }

            private bool Runs(ref MachineClock clock)
            {
                var building = clock.Owner;
                var offline = Offlines.HasEnabled(building) ? Offlines[building] : default;
                switch (clock.Run)
                {
                    case MachineRun.Powered:
                        return offline.Reasons == OfflineReason.None;
                    case MachineRun.Producing:
                        return !offline.HasReasonOtherThan(OfflineReason.Unpowered) && Queues.TryGetBuffer(building, out var queue) && !queue.IsEmpty;
                    case MachineRun.Firing:
                        return clock.Fired(Shots(building), DeltaTime);
                    default:
                        return true;
                }
            }

            // Any weapon or interceptor shot changes the sum, which wraps like each count.
            private byte Shots(Entity building)
            {
                byte shots = 0;
                if (Poses.TryGetBuffer(building, out var poses))
                {
                    foreach (var pose in poses)
                    {
                        shots += pose.Shots;
                    }
                }

                return shots;
            }
        }

        [BurstCompile]
        private partial struct PartJob : IJobEntity
        {
            [ReadOnly] public ComponentLookup<MachineClock> Clocks;
            [ReadOnly] public ComponentLookup<LocalToWorld> Places;
            public float3 Downwind;

            private void Execute(ref LocalTransform transform, in MachinePart part, in Parent parent)
            {
                if (!Clocks.TryGetComponent(part.Machine, out var clock))
                {
                    return;
                }

                var cycle = math.radians(part.Speed * clock.Time + part.Phase);
                switch (part.Motion)
                {
                    case MachineMotion.Spin:
                        transform.Rotation = math.mul(part.Rest, quaternion.AxisAngle(part.Axis, cycle));
                        break;
                    case MachineMotion.Swing:
                        var angle = math.radians(part.Offset + part.Amplitude * math.sin(cycle));
                        transform.Rotation = math.mul(part.Rest, quaternion.AxisAngle(part.Axis, angle));
                        break;
                    case MachineMotion.Slide:
                        var travel = part.Offset + part.Amplitude * math.sin(cycle);
                        transform.Position = part.RestPosition + part.Axis * travel;
                        break;
                    case MachineMotion.Raise:
                        var raised = math.radians(part.Amplitude) * part.Unfolded(clock.Ramp);
                        transform.Rotation = math.mul(part.Rest, quaternion.AxisAngle(part.Axis, raised));
                        break;
                    case MachineMotion.Extend:
                        transform.Position = part.RestPosition + part.Axis * (part.Amplitude * part.Unfolded(clock.Ramp));
                        break;
                    case MachineMotion.Downwind:
                        if (Places.TryGetComponent(parent.Value, out var frame))
                        {
                            transform.Rotation = part.FacingDownwind(math.rotate(math.inverse(frame.Rotation), Downwind));
                        }

                        break;
                    case MachineMotion.Point:
                        Point(ref transform, part, parent.Value);
                        break;
                    default:
                        Link(ref transform, part, parent.Value);
                        break;
                }
            }

            private void Point(ref LocalTransform transform, in MachinePart part, Entity parent)
            {
                if (Places.TryGetComponent(part.Aim, out var aim) & Places.TryGetComponent(parent, out var frame))
                {
                    transform.Rotation = part.Pointing(math.transform(math.inverse(frame.Value), aim.Position));
                }
            }

            private void Link(ref LocalTransform transform, in MachinePart part, Entity parent)
            {
                var ok = Places.TryGetComponent(part.Follow, out var follow) & Places.TryGetComponent(part.Aim, out var aim)
                    & Places.TryGetComponent(parent, out var frame);
                if (!ok)
                {
                    return;
                }

                var toLocal = math.inverse(frame.Value);
                var start = math.transform(toLocal, follow.Position);
                var direction = math.normalizesafe(math.transform(toLocal, aim.Position) - start, math.up());
                transform.Position = start;
                transform.Rotation = Rotations.FromTo(math.up(), direction);
            }
        }
    }
}
