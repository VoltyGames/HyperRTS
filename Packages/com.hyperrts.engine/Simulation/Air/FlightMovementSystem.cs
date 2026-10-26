using HyperRTS.Core;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Navigation;
using HyperRTS.Simulation.Transport;
using HyperRTS.Simulation.Units;
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
        private ComponentLookup<Docked> _docked;
        private ComponentLookup<Locomotion> _locomotions;
        private ComponentLookup<LocomotionState> _locomotionStates;

        public void OnCreate(ref SystemState state)
        {
            _givingWay = new GivingWay(ref state);
            _docked = state.GetComponentLookup<Docked>(true);
            _locomotions = state.GetComponentLookup<Locomotion>(true);
            _locomotionStates = state.GetComponentLookup<LocomotionState>();
            state.RequireForUpdate<SpatialIndex>();
            state.RequireForUpdate<NavGrid>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _givingWay.Update(ref state);
            _docked.Update(ref state);
            _locomotions.Update(ref state);
            _locomotionStates.Update(ref state);
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
                Docked = _docked,
                Locomotions = _locomotions,
                States = _locomotionStates,
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
                var step = Plan(transform, facing, speed.Value, flight, landed, destination.Value, moving, ref path,
                    waypoints, locomotion, ref drive, ref heading, crowd, velocity.Value);
                heading = locomotion.Aim(heading, facing, !moving.ValueRO, aim, aiming, DeltaTime);
                Fly(ref transform, from + step + (landed ? float2.zero : crowd.PushWithin(maxStep)), flight, landed);
                if (moving.ValueRO && crowd.ReachedGroup)
                {
                    path.Halt(moving, waypoints);
                }

                var reason = crowd.ReachedGroup ? ArrivalReason.AtGroupmate : ArrivalReason.Reached;
                progress.Report(wasMoving, moving.ValueRO, reason, ref arrival, arrived);
                if (remembers)
                {
                    drive.Hit(transform.Position.xz - from, step, false);
                    States[entity] = drive;
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

            /// <summary>This frame's step and facing before the crowd's push.</summary>
            private readonly float2 Plan(in LocalTransform transform, float2 facing, float topSpeed, in Flight flight,
                bool landed, float3 goal, EnabledRefRW<MoveDestination> moving, ref PathState path,
                DynamicBuffer<PathWaypoint> waypoints, in Locomotion locomotion, ref LocomotionState drive,
                ref float2 heading, in CrowdVisitor crowd, float2 velocity)
            {
                var position = transform.Position.xz;
                var leg = PathLeg.Along(position, goal.xz, waypoints, path);
                if (moving.ValueRO && locomotion.HasArrived(leg, position, facing, crowd.Radius))
                {
                    path.Halt(moving, waypoints);
                }

                if (moving.ValueRO)
                {
                    var steering = new Steering(crowd, velocity, topSpeed);
                    return steering.Drive(locomotion, ref drive, facing, ref heading, leg, DeltaTime);
                }

                return Loiter(transform.Rotation, flight, landed, topSpeed * DeltaTime, ref heading);
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
