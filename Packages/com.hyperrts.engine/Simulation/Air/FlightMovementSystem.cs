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
    /// Flies aircraft straight at their <see cref="MoveDestination"/> at their <see cref="Flight"/> altitude, steering round other aircraft.
    /// Idle ones that can't hover circle.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(MovementSystemGroup))]
    [UpdateAfter(typeof(PathfindingSystem))]
    [UpdateBefore(typeof(CargoFollowSystem))]
    public partial struct FlightMovementSystem : ISystem
    {
        private RightOfWay _rightOfWay;

        public void OnCreate(ref SystemState state)
        {
            _rightOfWay = new RightOfWay(ref state);
            state.RequireForUpdate<OwnerRelations>();
            state.RequireForUpdate<SpatialIndex>();
            state.RequireForUpdate<NavGrid>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _rightOfWay.Update(ref state, SystemAPI.GetSingleton<OwnerRelations>());
            var index = SystemAPI.GetSingleton<SpatialIndex>();
            var grid = SystemAPI.GetSingleton<NavGrid>();
            if (SystemAPI.Time.DeltaTime <= 0f)
            {
                return;
            }

            new FlyJob
            {
                Movers = new Movers
                {
                    Index = index,
                    RightOfWay = _rightOfWay,
                    Locomotions = SystemAPI.GetComponentLookup<Locomotion>(true),
                    States = SystemAPI.GetComponentLookup<LocomotionState>(false),
                    DeltaTime = SystemAPI.Time.DeltaTime,
                },
                Grid = grid,
                Docked = SystemAPI.GetComponentLookup<Docked>(true),
            }.ScheduleParallel();
        }

        [BurstCompile]
        [WithNone(typeof(Inside))]
        [WithPresent(typeof(MoveDestination), typeof(Arrival), typeof(FacingGoal))]
        private partial struct FlyJob : IJobEntity
        {
            public Movers Movers;
            [ReadOnly] public NavGrid Grid;
            [ReadOnly] public ComponentLookup<Docked> Docked;

            // `ref`, not `in`: each must share one writable handle with its EnabledRefRW.
            private void Execute(Entity entity, ref LocalTransform transform, in MovementSpeed speed, in NavAgent agent,
                in Owner owner, in Flight flight, ref MoveDestination destination,
                EnabledRefRW<MoveDestination> moving, ref PathState path, DynamicBuffer<PathWaypoint> waypoints,
                ref MoveProgress progress, ref Arrival arrival, EnabledRefRW<Arrival> arrived, ref Velocity velocity,
                ref FacingGoal aim, EnabledRefRW<FacingGoal> aiming)
            {
                var deltaTime = Movers.DeltaTime;
                var maxStep = speed.Value * deltaTime;
                var heading = float2.zero;
                var landed = Docked.HasEnabled(entity);
                var frame = Movers.Begin(entity, transform, agent, owner, destination, moving.ValueRO, speed.Value,
                    velocity.Value);
                var from = frame.From;
                var crowd = frame.Crowd;
                var leg = PathLeg.Along(from, destination.Value.xz, waypoints, path);
                if (moving.ValueRO && frame.Driver.HasArrived(leg))
                {
                    path.Halt(moving, waypoints);
                }

                var step = float2.zero;
                if (moving.ValueRO)
                {
                    step = new Steering(crowd, velocity.Value).Drive(ref frame.Driver, leg);
                    heading = frame.Driver.Facing;
                }
                else
                {
                    step = Loiter(transform.Rotation, flight, landed, frame.Driver.TopSpeed * deltaTime, ref heading);
                }

                heading = frame.Driver.Aim(heading, !moving.ValueRO, aim, aiming);
                Fly(ref transform, from + step + (landed ? float2.zero : crowd.PushWithin(maxStep)), flight, landed);
                if (moving.ValueRO && crowd.ReachedGroup)
                {
                    path.Halt(moving, waypoints);
                }

                var reason = crowd.ReachedGroup ? ArrivalReason.AtGroupmate : ArrivalReason.Reached;
                progress.Report(frame.WasMoving, moving.ValueRO, reason, ref arrival, arrived);
                Movers.End(ref frame, transform.Position.xz, step, false);

                Face(ref transform, heading);
                velocity.Value = (transform.Position.xz - from) / deltaTime;
            }

            /// <summary>Goes wherever it steers and climbs toward its altitude over the surface below.</summary>
            private readonly void Fly(ref LocalTransform transform, float2 wanted, in Flight flight, bool landed)
            {
                transform.Position.xz = wanted;
                var surface = Grid.SurfaceHeight(transform.Position);
                transform.Position.y = FlightMath.Climb(transform.Position.y, surface, flight, landed, Movers.DeltaTime);
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
