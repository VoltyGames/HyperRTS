using Unity.Collections;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Movement
{
    /// <summary>The neighbours round one mover and how deep a velocity would run into them.</summary>
    internal readonly struct Neighbourhood
    {
        private readonly AvoidanceList _neighbours;
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

        public int Count => _neighbours.Count;

        /// <summary>Metres of overlap over the time slice with a footprint on <paramref name="axis"/> moving at <paramref name="candidate"/>.</summary>
        public float Overlap(float2 axis, float2 candidate, float from, float time)
        {
            var overlap = 0f;
            for (var i = 0; i < _neighbours.Count; i++)
            {
                overlap += _neighbours[i].Overlap(Position, axis, _velocity, candidate, from, time);
            }

            return overlap;
        }
    }
}
