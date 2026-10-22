using Unity.Mathematics;

namespace HyperRTS.Simulation.Units
{
    /// <summary>
    /// A unit an <see cref="AvoidanceVisitor"/> keeps clear of, as a circle at the closest point of its capsule to the
    /// mover's, and the ORCA half-plane that does it.
    /// </summary>
    internal struct AvoidanceNeighbour
    {
        /// <summary>From the mover's closest point to the neighbour's.</summary>
        public float2 Offset;

        public float2 Velocity;

        /// <summary>Both radii plus the larger spacing.</summary>
        public float Reach;

        /// <summary>The mover's part of the avoiding: half when the other gives way too, all against one holding.</summary>
        public float Share;

        public float DistanceSq;

        /// <summary>Velocities for a mover at <paramref name="velocity"/> that stay clear for the time horizon.</summary>
        public readonly VelocityPlane Plane(float2 velocity, float timeHorizon, float deltaTime)
        {
            var relative = velocity - Velocity;
            var reachSq = Reach * Reach;
            if (DistanceSq <= reachSq)
            {
                // Already touching: part within the frame.
                return Cutoff(velocity, relative, 1f / deltaTime);
            }

            var w = relative - Offset / timeHorizon;
            var along = math.dot(w, Offset);
            var nearestIsCutoff = along < 0f && along * along > reachSq * math.lengthsq(w);
            if (nearestIsCutoff && !SteersRound(w, reachSq, timeHorizon))
            {
                return Cutoff(velocity, relative, 1f / timeHorizon);
            }

            return Leg(velocity, relative, w);
        }

        // Braking for a unit coming at the mover or standing still only stalls both, so it steers round instead.
        private readonly bool SteersRound(float2 w, float reachSq, float timeHorizon) =>
            math.lengthsq(w) * timeHorizon * timeHorizon < reachSq && math.dot(Velocity, Offset) <= 0f;

        private readonly VelocityPlane Cutoff(float2 velocity, float2 relative, float inverseTime)
        {
            var w = relative - Offset * inverseTime;
            var length = math.length(w);
            var unit = length > 1e-6f ? w / length : -math.normalize(Offset);
            var change = unit * (Reach * inverseTime - length);
            return new VelocityPlane { Point = velocity + change * Share, Direction = new float2(unit.y, -unit.x) };
        }

        // Along the cone's edge; dead ahead takes the right edge, so both units of a head-on pair keep right.
        private readonly VelocityPlane Leg(float2 velocity, float2 relative, float2 w)
        {
            var leg = math.sqrt(DistanceSq - Reach * Reach);
            var o = Offset;
            var direction = VelocityPlane.Cross(o, w) > 0f
                ? new float2(o.x * leg - o.y * Reach, o.x * Reach + o.y * leg) / DistanceSq
                : -new float2(o.x * leg + o.y * Reach, -o.x * Reach + o.y * leg) / DistanceSq;
            var change = math.dot(relative, direction) * direction - relative;
            return new VelocityPlane { Point = velocity + change * Share, Direction = direction };
        }
    }
}
