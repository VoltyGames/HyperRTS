using HyperRTS.Core;
using HyperRTS.Simulation.Animation;
using HyperRTS.Simulation.Common;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Presentation.Animation
{
    /// <summary>
    /// Runs machine clocks (paused while an owned-only machine is neutral) and poses their parts: spins, swings in
    /// step with the spin, and links that sit on one point aiming at another. Links read last frame's transforms.
    /// </summary>
    [BurstCompile]
    [WorldSystemFilter(SimulationWorlds.Presented)]
    [UpdateInGroup(typeof(PoseSystemGroup))]
    public partial struct MachineSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state) => state.RequireForUpdate<MachineClock>();

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            state.Dependency = new ClockJob
            {
                DeltaTime = SystemAPI.Time.DeltaTime,
                Factions = SystemAPI.GetComponentLookup<Faction>(true),
            }.ScheduleParallel(state.Dependency);
            state.Dependency = new PartJob
            {
                Clocks = SystemAPI.GetComponentLookup<MachineClock>(true),
                Places = SystemAPI.GetComponentLookup<LocalToWorld>(true),
            }.ScheduleParallel(state.Dependency);
        }

        [BurstCompile]
        private partial struct ClockJob : IJobEntity
        {
            public float DeltaTime;
            [ReadOnly] public ComponentLookup<Faction> Factions;

            private void Execute(Entity entity, ref MachineClock clock)
            {
                var owned = Factions.TryGetComponent(entity, out var faction) && faction.Value != Faction.Neutral;
                if (!clock.OwnedOnly || owned)
                {
                    clock.Time += DeltaTime;
                }
            }
        }

        [BurstCompile]
        private partial struct PartJob : IJobEntity
        {
            [ReadOnly] public ComponentLookup<MachineClock> Clocks;
            [ReadOnly] public ComponentLookup<LocalToWorld> Places;

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
                    default:
                        Link(ref transform, part, parent.Value);
                        break;
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
