using HyperRTS.Simulation.Common.Infantry;
using HyperRTS.Simulation.Navigation.Agents;
using HyperRTS.Simulation.Navigation.Grid;
using HyperRTS.Simulation.Navigation.Paths;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.Movement
{
    /// <summary>
    /// One ground or naval unit's frame: plan, crowd, walls and arrival.
    /// </summary>
    internal struct GroundMover
    {
        public Movers Movers;
        [ReadOnly] public NavGrid Grid;
        [ReadOnly] public ComponentLookup<Pace> Paces;

        public void Move(in MoverChunk chunk, int index)
        {
            var entity = chunk.Entities[index];
            var agent = chunk.Agents[index];
            // Aircraft fly in FlightMovementSystem, which this module can't name without a cycle.
            if (agent.Layer == NavLayer.Air)
            {
                return;
            }

            var top = chunk.Speeds[index].Value * (Paces.TryGetComponent(entity, out var pace) ? pace.Speed : 1f);
            // A frozen squad keeps its destination and goes on when it is free.
            if (top <= 0f)
            {
                chunk.Velocities[index].Value = float2.zero;
                return;
            }

            Drive(chunk, index, agent, top);
        }

        private void Drive(in MoverChunk chunk, int index, in NavAgent agent, float top)
        {
            ref var transform = ref chunk.Transforms[index];
            ref var path = ref chunk.Paths[index];
            var moving = chunk.Moving(index);
            var waypoints = chunk.Waypoints[index];
            var maxStep = top * Movers.DeltaTime;
            var frame = Movers.Begin(chunk, index, top);
            var from = frame.From;
            var facing = frame.Facing;
            ref readonly var crowd = ref frame.Crowd;
            var axis = facing * agent.HalfSegment;

            var capsule = new NavCapsule(agent.Access(Grid), axis);
            var step = Plan(ref frame, chunk.Destinations[index], moving, ref path, waypoints, capsule.Access);
            var heading = frame.Driver.Aim(frame.Heading, !moving.ValueRO, chunk.Aims[index], chunk.Aiming(index));

            // A building or wreck that lands on a unit leaves it sunk in walls, parked or not, and it drives out.
            var depth = capsule.Depth(from, axis);
            var escape = depth > NavTolerances.WallOverlap ? capsule.WayOut(from, maxStep) : float2.zero;
            var wanted = from + crowd.Within(step + escape + crowd.PushWithin(maxStep));
            Walk(ref transform, capsule, from, wanted, agent, crowd);

            var stalled = HasStalled(chunk, index, ref frame, capsule, depth);
            Arrive(chunk, index, ref frame, stalled, step, !transform.Position.xz.Equals(wanted));
            TurnOrBackOut(chunk, index, ref frame, capsule, facing, heading);
            chunk.Velocities[index].Value = (transform.Position.xz - from) / Movers.DeltaTime;
        }

        private readonly void Arrive(in MoverChunk chunk, int index, ref MoveFrame frame, bool stalled, float2 step,
            bool walled)
        {
            var moving = chunk.Moving(index);
            if (moving.ValueRO && (frame.Crowd.ReachedGroup || stalled))
            {
                ref var path = ref chunk.Paths[index];
                path.Halt(moving, chunk.Waypoints[index]);
            }

            var reason = stalled ? ArrivalReason.Stalled : ArrivalReason.Reached;
            if (frame.Crowd.ReachedGroup)
            {
                reason = ArrivalReason.AtGroupmate;
            }

            Movers.Finish(chunk, index, ref frame, reason, step, walled);
        }

        // A hull that can't turn where it stands backs out, which the next frame's state carries.
        private static void TurnOrBackOut(in MoverChunk chunk, int index, ref MoveFrame frame, in NavCapsule capsule,
            float2 facing, float2 heading)
        {
            var moving = chunk.Moving(index).ValueRO;
            ref var transform = ref chunk.Transforms[index];
            if (Face(ref transform, capsule, chunk.Agents[index], facing, heading, moving) || !moving || !frame.Remembers)
            {
                return;
            }

            frame.Driver.BackOutOfPivot();
            var drives = chunk.Drives;
            drives[index] = frame.Driver.State;
        }

        /// <summary>Whether the unit gave up on its way after this frame's walk, turning out of walls when it can.</summary>
        private readonly bool HasStalled(in MoverChunk chunk, int index, ref MoveFrame frame, in NavCapsule capsule,
            float depth)
        {
            var agent = chunk.Agents[index];
            var position = chunk.Transforms[index].Position.xz;
            ref readonly var crowd = ref frame.Crowd;
            var moving = chunk.Moving(index);
            var waypoints = chunk.Waypoints[index];
            ref var path = ref chunk.Paths[index];
            var hold = Hold(capsule, depth, frame.From, position, frame.Facing * agent.HalfSegment, crowd);
            var reading = new MoveProgress.Reading(position, frame.Facing, agent.Radius, chunk.Destinations[index].Value,
                waypoints, path);
            var stalled = moving.ValueRO && !crowd.ReachedGroup &&
                          chunk.Progresses[index].HasGivenUp(reading, ref path, hold, Movers.DeltaTime);

            if (stalled && frame.Remembers)
            {
                stalled = !chunk.Progresses[index].TryTurnOut(ref frame.Driver, hold);
            }

            return stalled;
        }

        // A long hull mustn't swing an end into a wall. A mover slides off by the end's sweep; false when it has to
        // stay unturned.
        private static bool Face(ref LocalTransform transform, in NavCapsule capsule, in NavAgent agent,
            float2 facing, float2 heading, bool moving)
        {
            if (math.all(heading == 0f))
            {
                return true;
            }

            var center = transform.Position.xz;
            var turned = quaternion.LookRotationSafe(new float3(heading.x, 0f, heading.y), math.up());
            var sweep = agent.HalfSegment * math.acos(math.clamp(math.dot(facing, heading), -1f, 1f));
            var turnedAxis = agent.SegmentAxis(turned);
            if (agent.HalfSegment <= 0f || capsule.CanTurn(center, turnedAxis))
            {
                transform.Rotation = turned;
                return true;
            }

            if (!moving || !capsule.TryTurnOff(center, turnedAxis, sweep, out var moved))
            {
                return false;
            }

            transform.Position.xz = moved;
            transform.Rotation = turned;
            return true;
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
        private static float2 Plan(ref MoveFrame frame, in MoveDestination destination,
            EnabledRefRW<MoveDestination> moving, ref PathState path, DynamicBuffer<PathWaypoint> waypoints,
            in NavAccess access)
        {
            ref var driver = ref frame.Driver;
            var leg = default(PathLeg);
            if (moving.ValueRO && !Follow(driver, access, destination, ref path, waypoints, out leg))
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
                var toGoal = math.normalizesafe(destination.Value.xz - driver.Position);
                frame.Heading = driver.TurnToward(destination.Backward ? -toGoal : toGoal);
                return driver.BrakeToStop();
            }

            var steering = new Steering(frame.Crowd, frame.Velocity);
            steering.AddWalls(access);
            var step = steering.Drive(ref driver, leg);
            frame.Heading = driver.Facing;
            return step;
        }

        /// <summary>Picks the leg to drive; false once the unit has arrived.</summary>
        private static bool Follow(in Driver driver, in NavAccess access, in MoveDestination destination,
            ref PathState path, DynamicBuffer<PathWaypoint> waypoints, out PathLeg leg)
        {
            var touch = math.max(driver.Radius * 0.5f, driver.TopSpeed * driver.DeltaTime);
            path.SkipCorners(access, driver.Position, touch, driver.LookAhead, waypoints);
            leg = PathLeg.Along(driver.Position, destination.Value.xz, waypoints, path);
            return !driver.HasArrived(leg);
        }
    }
}
