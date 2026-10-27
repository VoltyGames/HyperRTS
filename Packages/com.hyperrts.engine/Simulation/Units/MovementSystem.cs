using HyperRTS.Core;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Navigation;
using HyperRTS.Simulation.Transport;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.Units
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
        private GivingWay _givingWay;
        private ComponentLookup<Locomotion> _locomotions;
        private ComponentLookup<LocomotionState> _locomotionStates;

        public void OnCreate(ref SystemState state)
        {
            _givingWay = new GivingWay(ref state);
            _locomotions = state.GetComponentLookup<Locomotion>(true);
            _locomotionStates = state.GetComponentLookup<LocomotionState>();
            state.RequireForUpdate<SpatialIndex>();
            state.RequireForUpdate<NavGrid>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _givingWay.Update(ref state);
            _locomotions.Update(ref state);
            _locomotionStates.Update(ref state);
            var index = SystemAPI.GetSingleton<SpatialIndex>();
            var grid = SystemAPI.GetSingleton<NavGrid>();
            // A paused frame moves nothing and has no velocity to measure.
            if (SystemAPI.Time.DeltaTime <= 0f)
            {
                return;
            }

            new MoveJob
            {
                Index = index,
                Grid = grid,
                GivingWay = _givingWay,
                Locomotions = _locomotions,
                States = _locomotionStates,
                DeltaTime = SystemAPI.Time.DeltaTime,
            }.ScheduleParallel();
        }

        [BurstCompile]
        [WithNone(typeof(Inside))]
        [WithPresent(typeof(MoveDestination), typeof(Arrival), typeof(FacingGoal))]
        private partial struct MoveJob : IJobEntity
        {
            [ReadOnly] public SpatialIndex Index;
            [ReadOnly] public NavGrid Grid;
            public GivingWay GivingWay;
            [ReadOnly] public ComponentLookup<Locomotion> Locomotions;

            // Each unit writes only its own state.
            [NativeDisableParallelForRestriction] public ComponentLookup<LocomotionState> States;
            public float DeltaTime;

            // `ref`, not `in`: each must share one writable handle with its EnabledRefRW.
            private void Execute(Entity entity, ref LocalTransform transform, in MovementSpeed speed, in NavAgent agent,
                in Faction faction, ref MoveDestination destination, EnabledRefRW<MoveDestination> moving,
                ref PathState path, DynamicBuffer<PathWaypoint> waypoints, ref MoveProgress progress,
                ref Arrival arrival, EnabledRefRW<Arrival> arrived, ref Velocity velocity, ref FacingGoal aim,
                EnabledRefRW<FacingGoal> aiming)
            {
                // Aircraft fly in FlightMovementSystem, which this module can't name without a cycle.
                if (agent.Layer == NavLayer.Air)
                {
                    return;
                }

                var maxStep = speed.Value * DeltaTime;
                var heading = float2.zero;
                var locomotion = Locomotions.TryGetComponent(entity, out var own) ? own : Locomotion.Instant;
                // Instant handling needs no memory between frames, so units without it start each from rest.
                var remembers = States.TryGetComponent(entity, out var drive);
                if (!remembers)
                {
                    // Without state, last frame's movement stands in for last frame's steering choice.
                    drive.LastChoice = velocity.Value;
                }

                // Read before arrival can clear it, as the index saw it, so both sides of a contact agree.
                var wasMoving = moving.ValueRO;
                var from = transform.Position.xz;
                var facing = math.normalizesafe(transform.Forward().xz);
                var axis = facing * agent.HalfSegment;
                var crowd = new CrowdVisitor(entity, from, facing, agent, faction.Value, wasMoving, destination,
                    GivingWay);
                Index.QueryHulls(crowd.Center, crowd.Reach(speed.Value), ref crowd);

                var capsule = new NavCapsule(agent.Access(Grid), axis);
                var driver = new Driver(locomotion, agent, speed.Value, DeltaTime)
                {
                    Position = from, Facing = facing, State = drive,
                };
                var step = Plan(ref driver, ref heading, destination.Value, moving, ref path, waypoints, crowd,
                    velocity.Value, capsule.Access);
                heading = driver.Aim(heading, !moving.ValueRO, aim, aiming);

                // A building or wreck that lands on a unit leaves it sunk in walls, parked or not, and it drives out.
                var depth = capsule.Depth(from, axis);
                var sunk = depth > NavTolerances.WallOverlap;
                var escape = sunk ? capsule.WayOut(from, maxStep) : float2.zero;
                var wanted = from + crowd.Within(step + escape + crowd.PushWithin(maxStep));
                Walk(ref transform, capsule, from, wanted, agent, crowd);

                var hold = Hold(capsule, depth, from, transform.Position.xz, axis, crowd);
                var stalled = moving.ValueRO && !crowd.ReachedGroup &&
                              progress.HasGivenUp(transform.Position.xz, facing, agent, destination.Value, waypoints,
                                  ref path, hold, DeltaTime);

                // A planned manoeuvre stuck in walls replans its way out rather than turning out.
                if (stalled && remembers && !PathState.IsPlannedManoeuvre(waypoints))
                {
                    stalled = !progress.TryTurnOut(ref driver, hold);
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

                progress.Report(wasMoving, moving.ValueRO, reason, ref arrival, arrived);
                if (remembers)
                {
                    driver.State.Hit(transform.Position.xz - from, step, !transform.Position.xz.Equals(wanted));
                    States[entity] = driver.State;
                }

                Face(ref transform, capsule, agent, facing, heading, moving.ValueRO);
                velocity.Value = (transform.Position.xz - from) / DeltaTime;
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
            private readonly float2 Plan(ref Driver driver, ref float2 heading, float3 goal,
                EnabledRefRW<MoveDestination> moving, ref PathState path, DynamicBuffer<PathWaypoint> waypoints,
                in CrowdVisitor crowd, float2 velocity, in NavAccess access)
            {
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
                    heading = driver.TurnToward(math.normalizesafe(goal.xz - driver.Position));
                    return driver.BrakeToStop();
                }

                var steering = new Steering(crowd, velocity);
                steering.AddWalls(access);
                var step = steering.Drive(ref driver, leg);
                heading = driver.Facing;
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
