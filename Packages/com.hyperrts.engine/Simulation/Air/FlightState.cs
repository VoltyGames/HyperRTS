using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Air
{
    /// <summary>A <see cref="Flight"/> aircraft's motion between frames. Its nose is its rotation.</summary>
    public struct FlightState : IComponentData
    {
        /// <summary>Metres per second it flies over the ground, without the crowd's push.</summary>
        public float2 Velocity;

        /// <summary>Metres per second it climbs; negative while it descends.</summary>
        public float ClimbRate;

        // Seconds to reach the full climb speed from level flight.
        private const float ClimbEase = 1f;

        /// <summary>
        /// Speeds up or brakes toward <paramref name="wanted"/> along its course and turns its course toward it no
        /// harder than its bank allows, so it flies arcs at speed and can turn on the spot when slow.
        /// </summary>
        public void Accelerate(in Flight flight, float2 wanted, float topSpeed, float deltaTime)
        {
            var along = math.normalizesafe(Velocity, math.normalizesafe(wanted));
            var across = new float2(-along.y, along.x);
            var change = wanted - Velocity;
            var forward = math.clamp(math.dot(change, along), -flight.Braking * deltaTime, flight.Acceleration * deltaTime);
            var turn = flight.TurnAcceleration * deltaTime;
            var side = math.clamp(math.dot(change, across), -turn, turn);
            Velocity += along * forward + across * side;
            var speed = math.length(Velocity);
            Velocity *= speed > topSpeed ? topSpeed / speed : 1f;
        }

        /// <summary>
        /// Height after one frame of climbing toward <paramref name="goal"/>, easing in and out of the climb and
        /// never below <paramref name="surface"/>.
        /// </summary>
        public float Climb(float height, float goal, float surface, in Flight flight, float deltaTime)
        {
            var gap = goal - height;
            var ease = flight.ClimbSpeed / ClimbEase;
            // Fastest climb that can still level off at the goal.
            var wanted = math.sign(gap) * math.min(flight.ClimbSpeed, math.sqrt(2f * ease * math.abs(gap)));
            ClimbRate += math.clamp(wanted - ClimbRate, -ease * deltaTime, ease * deltaTime);
            var step = ClimbRate * deltaTime;
            if (gap * step > 0f && math.abs(step) >= math.abs(gap))
            {
                ClimbRate = 0f;
                return math.max(surface, goal);
            }

            if (height + step < surface)
            {
                ClimbRate = math.max(ClimbRate, 0f);
                return surface;
            }

            return height + step;
        }
    }
}
