using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Movement;
using HyperRTS.Simulation.Navigation.Agents;
using HyperRTS.Simulation.Navigation.Grid;
using HyperRTS.Simulation.Navigation.Paths;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.Air
{
    /// <summary>
    /// One aircraft's frame: steer, fly its <see cref="FlightState"/> toward the steered velocity, climb and arrive.
    /// </summary>
    internal struct FlightMover
    {
        public Movers Movers;
        [ReadOnly] public NavGrid Grid;
        [ReadOnly] public ComponentLookup<Docked> Docked;

        // Slowest it closes on its goal, so the braking curve still arrives.
        private const float Crawl = 0.5f;

        // Share of its speed it flies off its nose while it turns toward the way it means to go.
        private const float SlipShare = 0.25f;

        // Above this share of its top speed the nose follows the course, as in forward flight.
        private const float ForwardFlightShare = 0.35f;

        // Seconds ahead it reads the ground, so it climbs before a ridge rather than at it.
        private const float LookAheadSeconds = 2f;

        /// <summary>Moves one aircraft; <paramref name="called"/> when a passenger waits to board it.</summary>
        public void Fly(in MoverChunk chunk, int index, in Flight flight, ref FlightState state, bool called)
        {
            ref var transform = ref chunk.Transforms[index];
            ref var path = ref chunk.Paths[index];
            var moving = chunk.Moving(index);
            var waypoints = chunk.Waypoints[index];
            var deltaTime = Movers.DeltaTime;
            var landed = IsLanded(chunk.Entities[index], moving.ValueRO, flight, called);
            var frame = Movers.Begin(chunk, index, chunk.Speeds[index].Value);
            var from = frame.From;
            var leg = PathLeg.Along(from, chunk.Destinations[index].Value.xz, waypoints, path);
            if (moving.ValueRO && frame.Driver.HasArrived(leg))
            {
                path.Halt(moving, waypoints);
            }

            var wanted = Wanted(chunk, index, ref frame, leg, flight, landed);
            state.Accelerate(flight, wanted, frame.Driver.TopSpeed, deltaTime);
            var step = state.Velocity * deltaTime;
            var push = landed ? float2.zero : frame.Crowd.PushWithin(frame.Driver.TopSpeed * deltaTime);
            transform.Position.xz = from + step + push;
            transform.Position.y = Climb(transform.Position, flight, ref state, landed);
            if (moving.ValueRO && frame.Crowd.ReachedGroup)
            {
                path.Halt(moving, waypoints);
            }

            var reason = frame.Crowd.ReachedGroup ? ArrivalReason.AtGroupmate : ArrivalReason.Reached;
            Movers.Finish(chunk, index, ref frame, reason, step, false);
            var course = landed ? float2.zero : Course(state, wanted, frame.Driver.TopSpeed);
            var toward = frame.Driver.Aim(course, !moving.ValueRO, chunk.Aims[index], chunk.Aiming(index));
            Face(ref transform, frame.Facing, toward, flight);
            chunk.Velocities[index].Value = (transform.Position.xz - from) / deltaTime;
        }

        // On its pad, or down for a passenger waiting to board: only an aircraft that hovers, while it stands.
        private readonly bool IsLanded(Entity aircraft, bool moving, in Flight flight, bool called)
        {
            if (Docked.HasEnabled(aircraft))
            {
                return true;
            }

            return !moving && flight.LoiterRadius <= 0f && called;
        }

        /// <summary>
        /// The velocity it means to fly: the steered one while moving, braking for the goal and slow until its nose
        /// comes round, else the idle loiter.
        /// </summary>
        private readonly float2 Wanted(in MoverChunk chunk, int index, ref MoveFrame frame, in PathLeg leg,
            in Flight flight, bool landed)
        {
            var top = frame.Driver.TopSpeed;
            if (!chunk.Moving(index).ValueRO)
            {
                return Loiter(chunk.Transforms[index].Rotation, flight, landed, top * Movers.DeltaTime) * top;
            }

            // The nose is flown below, so the driver steers as if already lined up and never stands to pivot.
            frame.Driver.Facing = leg.Direction;
            var drive = new Steering(frame.Crowd, chunk.Velocities[index].Value).Drive(ref frame.Driver, leg);
            var wanted = drive / Movers.DeltaTime;
            var speed = math.length(wanted);
            if (speed < 1e-4f)
            {
                return float2.zero;
            }

            var direction = wanted / speed;
            var left = math.max(0f, leg.Remaining - NavTolerances.Arrive(frame.Driver.Radius));
            var arriving = math.max(Crawl, math.sqrt(2f * flight.Braking * left));
            var lined = math.max(SlipShare, math.dot(frame.Facing, direction));
            return direction * math.min(speed, arriving) * lined;
        }

        // In forward flight the nose follows the course; slower it points where it means to go, or holds.
        private static float2 Course(in FlightState state, float2 wanted, float topSpeed)
        {
            var speed = math.length(state.Velocity);
            return speed > topSpeed * ForwardFlightShare ? state.Velocity / speed : math.normalizesafe(wanted);
        }

        /// <summary>Climbs toward its altitude over the highest ground below or just ahead, or down onto it when landed.</summary>
        private readonly float Climb(float3 position, in Flight flight, ref FlightState state, bool landed)
        {
            var surface = Grid.SurfaceHeight(position);
            if (landed)
            {
                return state.Climb(position.y, surface, surface, flight, Movers.DeltaTime);
            }

            var ahead = position + new float3(state.Velocity.x, 0f, state.Velocity.y) * LookAheadSeconds;
            var goal = math.max(surface, Grid.SurfaceHeight(ahead)) + flight.Altitude;
            return state.Climb(position.y, goal, surface, flight, Movers.DeltaTime);
        }

        private readonly void Face(ref LocalTransform transform, float2 facing, float2 toward, in Flight flight)
        {
            if (math.all(toward == 0f))
            {
                return;
            }

            var nose = flight.Yaw(facing, math.normalize(toward), Movers.DeltaTime);
            transform.Rotation = quaternion.LookRotationSafe(new float3(nose.x, 0f, nose.y), math.up());
        }

        /// <summary>An idle aircraft that can't hover keeps flying a circle; one that can stays put.</summary>
        private static float2 Loiter(quaternion rotation, in Flight flight, bool landed, float maxStep)
        {
            if (flight.LoiterRadius <= 0f || landed)
            {
                return float2.zero;
            }

            return flight.Loiter(rotation, maxStep);
        }
    }
}
