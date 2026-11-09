using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Common.Infantry;
using HyperRTS.Simulation.Common.Players;
using HyperRTS.Simulation.Navigation.Agents;
using HyperRTS.Simulation.Navigation.Drive;
using HyperRTS.Simulation.Navigation.Grid;
using HyperRTS.Simulation.Navigation.Paths;
using HyperRTS.Simulation.Navigation.Spatial;
using HyperRTS.Simulation.Transport;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Profiling;
using Unity.Transforms;

namespace HyperRTS.Simulation.Movement
{
    /// <summary>
    /// Moves ground and naval units along their <see cref="PathWaypoint"/>s toward an enabled
    /// <see cref="MoveDestination"/>, and when the move ends by itself disables it and enables <see cref="Arrival"/>.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(MovementSystemGroup))]
    [UpdateAfter(typeof(PathfindingSystem))]
    [UpdateBefore(typeof(CargoFollowSystem))]
    public partial struct MovementSystem : ISystem
    {
        private static readonly ProfilerMarker MoveMarker = new("MovementSystem.Move");

        private GivingWay _givingWay;

        public void OnCreate(ref SystemState state)
        {
            _givingWay = new GivingWay(ref state);
            state.RequireForUpdate<SpatialIndex>();
            state.RequireForUpdate<NavGrid>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _givingWay.Update(ref state);
            var index = SystemAPI.GetSingleton<SpatialIndex>();
            var grid = SystemAPI.GetSingleton<NavGrid>();
            // A paused frame moves nothing and has no velocity to measure.
            if (SystemAPI.Time.DeltaTime <= 0f)
            {
                return;
            }

            new MoveJob
            {
                Movers = new Movers
                {
                    Index = index,
                    GivingWay = _givingWay,
                    Locomotions = SystemAPI.GetComponentLookup<Locomotion>(true),
                    States = SystemAPI.GetComponentLookup<LocomotionState>(false),
                    DeltaTime = SystemAPI.Time.DeltaTime,
                },
                Grid = grid,
                Paces = SystemAPI.GetComponentLookup<Pace>(true),
            }.ScheduleParallel();
        }

        [BurstCompile]
        [WithNone(typeof(Inside))]
        [WithPresent(typeof(MoveDestination), typeof(Arrival), typeof(FacingGoal))]
        private partial struct MoveJob : IJobEntity, IJobEntityChunkBeginEnd
        {
            public Movers Movers;
            [ReadOnly] public NavGrid Grid;
            [ReadOnly] public ComponentLookup<Pace> Paces;

            // Per chunk, not per unit, so the marker costs little and the profiler stays readable.
            public bool OnChunkBegin(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask,
                in v128 chunkEnabledMask)
            {
                MoveMarker.Begin();
                return true;
            }

            public void OnChunkEnd(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask,
                in v128 chunkEnabledMask, bool chunkWasExecuted) => MoveMarker.End();

            // `ref`, not `in`: each must share one writable handle with its EnabledRefRW.
            private void Execute(Entity entity, ref LocalTransform transform, in MovementSpeed speed, in NavAgent agent,
                in Owner owner, ref MoveDestination destination, EnabledRefRW<MoveDestination> moving,
                ref PathState path, DynamicBuffer<PathWaypoint> waypoints, ref MoveProgress progress,
                ref Arrival arrival, EnabledRefRW<Arrival> arrived, ref Velocity velocity, ref FacingGoal aim,
                EnabledRefRW<FacingGoal> aiming)
            {
                // Aircraft fly in FlightMovementSystem, which this module can't name without a cycle.
                if (agent.Layer == NavLayer.Air)
                {
                    return;
                }

                var top = speed.Value * (Paces.TryGetComponent(entity, out var pace) ? pace.Speed : 1f);
                // A frozen squad keeps its destination and goes on when it is free.
                if (top <= 0f)
                {
                    velocity.Value = float2.zero;
                    return;
                }

                var deltaTime = Movers.DeltaTime;
                var maxStep = top * deltaTime;
                var frame = Movers.Begin(entity, transform, agent, owner, destination, moving.ValueRO, top,
                    velocity.Value);
                var from = frame.From;
                var facing = frame.Facing;
                var crowd = frame.Crowd;
                var axis = facing * agent.HalfSegment;

                var capsule = new NavCapsule(agent.Access(Grid), axis);
                var step = Plan(ref frame, destination.Value, moving, ref path, waypoints, capsule.Access);
                var heading = frame.Driver.Aim(frame.Heading, !moving.ValueRO, aim, aiming);

                // A building or wreck that lands on a unit leaves it sunk in walls, parked or not, and it drives out.
                var depth = capsule.Depth(from, axis);
                var sunk = depth > NavTolerances.WallOverlap;
                var escape = sunk ? capsule.WayOut(from, maxStep) : float2.zero;
                var wanted = from + crowd.Within(step + escape + crowd.PushWithin(maxStep));
                Walk(ref transform, capsule, from, wanted, agent, crowd);

                var hold = Hold(capsule, depth, from, transform.Position.xz, axis, crowd);
                var reading = new MoveProgress.Reading(transform.Position.xz, facing, agent.Radius, destination.Value,
                    waypoints, path);
                var stalled = moving.ValueRO && !crowd.ReachedGroup &&
                              progress.HasGivenUp(reading, waypoints, ref path, hold, deltaTime);

                // A planned manoeuvre stuck in walls replans its way out rather than turning out.
                if (stalled && frame.Remembers && !PathState.IsPlannedManoeuvre(waypoints))
                {
                    stalled = !progress.TryTurnOut(ref frame.Driver, hold);
                }

                if (moving.ValueRO && (crowd.ReachedGroup || stalled))
                {
                    path.Halt(moving, waypoints);
                }

                var reason = stalled ? ArrivalReason.Stalled : ArrivalReason.Reached;
                if (crowd.ReachedGroup)
                {
                    reason = ArrivalReason.AtGroupmate;
                }

                progress.Report(frame.WasMoving, moving.ValueRO, reason, ref arrival, arrived);
                Movers.End(ref frame, transform.Position.xz, step, !transform.Position.xz.Equals(wanted));

                Face(ref transform, capsule, agent, facing, heading, moving.ValueRO);
                velocity.Value = (transform.Position.xz - from) / deltaTime;
            }

            // A long hull mustn't swing an end into a wall. A mover slides off by the end's sweep, else stays unturned.
            private static void Face(ref LocalTransform transform, in NavCapsule capsule, in NavAgent agent,
                float2 facing, float2 heading, bool moving)
            {
                if (math.all(heading == 0f))
                {
                    return;
                }

                var center = transform.Position.xz;
                var turned = quaternion.LookRotationSafe(new float3(heading.x, 0f, heading.y), math.up());
                var sweep = agent.HalfSegment * math.acos(math.clamp(math.dot(facing, heading), -1f, 1f));
                var turnedAxis = agent.SegmentAxis(turned);
                if (agent.HalfSegment <= 0f || capsule.CanTurn(center, turnedAxis))
                {
                    transform.Rotation = turned;
                }
                else if (moving && capsule.TryTurnOff(center, turnedAxis, sweep, out var moved))
                {
                    transform.Position.xz = moved;
                    transform.Rotation = turned;
                }
            }

            // Traffic clears, so only a unit with no neighbour near counts as wedged.
            private static MoveProgress.WallHold Hold(in NavCapsule capsule, float depth, float2 from, float2 to,
                float2 axis, in CrowdVisitor crowd)
            {
                if (depth > NavTolerances.WallOverlap)
                {
                    return capsule.Depth(to, axis) >= depth ? MoveProgress.WallHold.Sunk : MoveProgress.WallHold.None;
                }

                var standing = math.distancesq(to, from) < NavTolerances.StandingStep * NavTolerances.StandingStep;
                var wedged = depth > 0f && standing && !crowd.HasNeighbours;
                return wedged ? MoveProgress.WallHold.Wedged : MoveProgress.WallHold.None;
            }

            // A wall's slide can turn the move back into a neighbour, and then the unit stands.
            private readonly void Walk(ref LocalTransform transform, in NavCapsule capsule, float2 from, float2 wanted,
                in NavAgent agent, in CrowdVisitor crowd)
            {
                var to = capsule.Constrain(from, wanted);
                transform.Position.xz = crowd.Keeps(to - from) ? to : from;
                transform.Position.y = Grid.HeightFor(transform.Position, agent.Layer);
            }

            /// <summary>This frame's step and facing before the crowd's push and the walls have their say.</summary>
            private static float2 Plan(ref MoveFrame frame, float3 goal, EnabledRefRW<MoveDestination> moving,
                ref PathState path, DynamicBuffer<PathWaypoint> waypoints, in NavAccess access)
            {
                ref var driver = ref frame.Driver;
                var leg = default(PathLeg);
                if (moving.ValueRO && !Follow(driver, access, goal, ref path, waypoints, out leg))
                {
                    path.Halt(moving, waypoints);
                }

                if (!moving.ValueRO)
                {
                    return driver.BrakeToStop();
                }

                // No corners yet: stop and face the goal rather than drive blind at it.
                if (path.Status != PathStatus.Ready && waypoints.Length == 0)
                {
                    frame.Heading = driver.TurnToward(math.normalizesafe(goal.xz - driver.Position));
                    return driver.BrakeToStop();
                }

                var steering = new Steering(frame.Crowd, frame.Velocity);
                steering.AddWalls(access);
                var step = steering.Drive(ref driver, leg);
                frame.Heading = driver.Facing;
                return step;
            }

            /// <summary>Picks the leg to drive; false once the unit has arrived.</summary>
            private static bool Follow(in Driver driver, in NavAccess access, float3 goal, ref PathState path,
                DynamicBuffer<PathWaypoint> waypoints, out PathLeg leg)
            {
                var touch = math.max(driver.Radius * 0.5f, driver.TopSpeed * driver.DeltaTime);
                path.SkipCorners(access, driver.Position, touch, driver.LookAhead, waypoints);
                leg = PathLeg.Along(driver.Position, goal.xz, waypoints, path).Manoeuvre(driver.Position, waypoints,
                    Locomotion.TightLookAhead(driver.State.Speed), driver.Chassis);
                return !driver.HasArrived(leg);
            }
        }
    }
}
