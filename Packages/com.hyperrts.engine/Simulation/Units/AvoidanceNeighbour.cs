using HyperRTS.Simulation.Navigation;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Units
{
    /// <summary>A unit a <see cref="CrowdVisitor"/> mover steers round: its capsule, velocity and the mover's share.</summary>
    internal struct AvoidanceNeighbour
    {
        public float2 Position;

        /// <summary>Half its core segment; zero for round footprints.</summary>
        public float2 SegmentAxis;

        public float2 Velocity;

        /// <summary>Both radii plus the larger spacing.</summary>
        public float Reach;

        /// <summary>The mover's part of the avoiding: half when the other gives way too, all against one holding.</summary>
        public float Share;

        public float DistanceSq;

        /// <summary>How deep the footprints already overlap; the push parts that, so steering only avoids worse.</summary>
        public float Contact;

        /// <summary>
        /// How deep the two footprints overlap on straight lines, where their centres pass closest between
        /// <paramref name="from"/> and <paramref name="to"/> seconds, so a fast pair can't slip through between
        /// samples. The mover changes its velocity by only its share of the step to <paramref name="candidate"/>, as
        /// in RVO, trusting the other with the rest.
        /// </summary>
        public readonly float Overlap(float2 position, float2 axis, float2 velocity, float2 candidate, float from,
            float to)
        {
            var shared = velocity + (candidate - velocity) / Share;
            var relative = shared - Velocity;
            var speedSq = math.lengthsq(relative);
            var closest = speedSq > 1e-6f ? -math.dot(position - Position, relative) / speedSq : to;
            var time = math.clamp(closest, from, to);
            var self = position + shared * time;
            var offset = NavCapsule.ClosestOffset(self, axis, Position + Velocity * time, SegmentAxis);
            return math.max(0f, Reach - math.length(offset) - Contact);
        }
    }
}
