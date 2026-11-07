using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Common.Players;
using HyperRTS.Simulation.Navigation.Agents;
using HyperRTS.Simulation.Navigation.Grid;
using HyperRTS.Simulation.Navigation.Paths;
using HyperRTS.Simulation.Navigation.Spatial;
using HyperRTS.Simulation.Transport;
using HyperRTS.Simulation.Movement;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.Air
{
    /// <summary>
    /// Flies aircraft straight at their <see cref="MoveDestination"/>, over everything, steering round other aircraft
    /// and climbing to their <see cref="Flight"/> altitude (landing while <see cref="Docked"/>, where the crowd can't
    /// shift them off the pad). Idle ones that can't hover circle. Ends moves as <see cref="MovementSystem"/> does.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(MovementSystemGroup))]
    [UpdateAfter(typeof(PathfindingSystem))]
    [UpdateBefore(typeof(CargoFollowSystem))]
    public partial struct FlightMovementSystem : ISystem
    {
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
            if (SystemAPI.Time.DeltaTime <= 0f)
            {
                return;
            }

            new FlyJob
            {
                Index = index,
                Grid = grid,
                GivingWay = _givingWay,
                Docked = SystemAPI.GetComponentLookup<Docked>(true),
                Locomotions = SystemAPI.GetComponentLookup<Locomotion>(true),
                States = SystemAPI.GetComponentLookup<LocomotionState>(false),
                DeltaTime = SystemAPI.Time.DeltaTime,
            }.ScheduleParallel();
        }

        [BurstCompile]
        [WithNone(typeof(Inside))]
        [WithPresent(typeof(MoveDestination), typeof(Arrival), typeof(FacingGoal))]
        private partial struct FlyJob : IJobEntity
        {
            [ReadOnly] public SpatialIndex Index;
            [ReadOnly] public NavGrid Grid;
            public GivingWay GivingWay;
            [ReadOnly] public ComponentLookup<Docked> Docked;
            [ReadOnly] public ComponentLookup<Locomotion> Locomotions;

            // Each aircraft writes only its own state.
            [NativeDisableParallelForRestriction] public ComponentLookup<LocomotionState> States;
            public float DeltaTime;

            // `ref`, not `in`: each must share one writable handle with its EnabledRefRW.
            private void Execute(Entity entity, ref LocalTransform transform, in MovementSpeed speed, in NavAgent agent,
                in Faction faction, in Flight flight, ref MoveDestination destination,
                EnabledRefRW<MoveDestination> moving, ref PathState path, DynamicBuffer<PathWaypoint> waypoints,
                ref MoveProgress progress, ref Arrival arrival, EnabledRefRW<Arrival> arrived, ref Velocity velocity,
                ref FacingGoal aim, EnabledRefRW<FacingGoal> aiming)
            {
                var maxStep = speed.Value * DeltaTime;
                var heading = float2.zero;
                var locomotion = Locomotions.TryGetComponent(entity, out var own) ? own : Locomotion.Instant;
                var remembers = States.TryGetComponent(entity, out var drive);
                if (!remembers)
                {
                    // Without state, last frame's movement stands in for last frame's steering choice.
                    drive.LastChoice = velocity.Value;
                }

                var wasMoving = moving.ValueRO;
                var landed = Docked.HasEnabled(entity);
                var from = transform.Position.xz;
                var facing = math.normalizesafe(transform.Forward().xz);
                var crowd = new CrowdVisitor(entity, from, facing, agent, faction.Value,
                    wasMoving, destination, GivingWay);
                Index.QueryHulls(crowd.Center, crowd.Reach(speed.Value), ref crowd);
                var driver = new Driver(locomotion, agent, speed.Value, DeltaTime)
                {
                    Position = from, Facing = facing, State = drive,
                };
                var leg = PathLeg.Along(driver.Position, destination.Value.xz, waypoints, path);
                if (moving.ValueRO && driver.HasArrived(leg))
                {
                    path.Halt(moving, waypoints);
                }

                var step = float2.zero;
                if (moving.ValueRO)
                {
                    step = new Steering(crowd, velocity.Value).Drive(ref driver, leg);
                    heading = driver.Facing;
                }
                else
                {
                    step = Loiter(transform.Rotation, flight, landed, driver.TopSpeed * DeltaTime, ref heading);
                }

                heading = driver.Aim(heading, !moving.ValueRO, aim, aiming);
                Fly(ref transform, from + step + (landed ? float2.zero : crowd.PushWithin(maxStep)), flight, landed);
                if (moving.ValueRO && crowd.ReachedGroup)
                {
                    path.Halt(moving, waypoints);
                }

                var reason = crowd.ReachedGroup ? ArrivalReason.AtGroupmate : ArrivalReason.Reached;
                progress.Report(wasMoving, moving.ValueRO, reason, ref arrival, arrived);
                if (remembers)
                {
                    driver.State.Hit(transform.Position.xz - from, step, false);
                    States[entity] = driver.State;
                }

                Face(ref transform, heading);
                velocity.Value = (transform.Position.xz - from) / DeltaTime;
            }

            /// <summary>Goes wherever it steers and climbs toward its altitude over the surface below.</summary>
            private readonly void Fly(ref LocalTransform transform, float2 wanted, in Flight flight, bool landed)
            {
                transform.Position.xz = wanted;
                var surface = Grid.SurfaceHeight(transform.Position);
                transform.Position.y = FlightMath.Climb(transform.Position.y, surface, flight, landed, DeltaTime);
            }

            private static void Face(ref LocalTransform transform, float2 heading)
            {
                if (math.any(heading != 0f))
                {
                    transform.Rotation = quaternion.LookRotationSafe(new float3(heading.x, 0f, heading.y), math.up());
                }
            }

            /// <summary>An idle aircraft that can't hover keeps flying a circle; one that can stays put.</summary>
            private static float2 Loiter(quaternion rotation, in Flight flight, bool landed, float maxStep,
                ref float2 heading)
            {
                if (flight.LoiterRadius <= 0f || landed)
                {
                    return float2.zero;
                }

                heading = FlightMath.Loiter(rotation, maxStep, flight.LoiterRadius);
                return heading * maxStep;
            }
        }
    }
}
