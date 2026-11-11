using Unity.Collections;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Movement
{
    /// <summary>The neighbours round one mover and how deep a velocity would run into them.</summary>
    internal readonly struct Neighbourhood
    {
        private readonly FixedList512Bytes<AvoidanceNeighbour> _neighbours;
        private readonly float2 _velocity;

        public readonly float2 Position;
        public readonly float2 Axis;

        public Neighbourhood(in CrowdVisitor crowd, float2 velocity)
        {
            _neighbours = crowd.Neighbours;
            _velocity = velocity;
            Position = crowd.Position;
            Axis = crowd.SegmentAxis;
        }

        public int Count => _neighbours.Length;

        /// <summary>Metres of overlap over the time slice with a footprint on <paramref name="axis"/> moving at <paramref name="candidate"/>.</summary>
        public float Overlap(float2 axis, float2 candidate, float from, float time)
        {
            var overlap = 0f;
            foreach (var neighbour in _neighbours)
            {
                overlap += neighbour.Overlap(Position, axis, _velocity, candidate, from, time);
            }

            return overlap;
        }
    }
}
