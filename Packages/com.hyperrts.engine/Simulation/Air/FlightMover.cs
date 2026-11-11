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
    /// One aircraft's frame: steer, climb and arrive.
    /// </summary>
    internal struct FlightMover
    {
        public Movers Movers;
        [ReadOnly] public NavGrid Grid;
        [ReadOnly] public ComponentLookup<Docked> Docked;

        public void Fly(in MoverChunk chunk, int index, in Flight flight)
        {
            ref var transform = ref chunk.Transforms[index];
            ref var velocity = ref chunk.Velocities[index];
            ref var path = ref chunk.Paths[index];
            var moving = chunk.Moving(index);
            var waypoints = chunk.Waypoints[index];
            var speed = chunk.Speeds[index];
            var deltaTime = Movers.DeltaTime;
            var maxStep = speed.Value * deltaTime;
            var heading = float2.zero;
            var landed = Docked.HasEnabled(chunk.Entities[index]);
            var frame = Movers.Begin(chunk, index, speed.Value);
            var from = frame.From;
            var crowd = frame.Crowd;
            var leg = PathLeg.Along(from, chunk.Destinations[index].Value.xz, waypoints, path);
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

            heading = frame.Driver.Aim(heading, !moving.ValueRO, chunk.Aims[index], chunk.Aiming(index));
            Fly(ref transform, from + step + (landed ? float2.zero : crowd.PushWithin(maxStep)), flight, landed);
            if (moving.ValueRO && crowd.ReachedGroup)
            {
                path.Halt(moving, waypoints);
            }

            var reason = crowd.ReachedGroup ? ArrivalReason.AtGroupmate : ArrivalReason.Reached;
            Movers.Finish(chunk, index, ref frame, reason, step, false);

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
