using Unity.Entities;

namespace HyperRTS.Simulation.Units
{
    /// <summary>
    /// The ground contact of a vehicle's tracks or wheels, for the ruts and flattened grass presentation draws behind
    /// it. Authoring data only: nothing in the simulation reads it.
    /// </summary>
    public struct TrackMarks : IComponentData
    {
        /// <summary>Width of one track or tyre in metres.</summary>
        public float Width;

        /// <summary>Distance between the left and right track centres in metres.</summary>
        public float Gauge;

        /// <summary>How deep the ruts read, 0 to 1 (light car about 0.3, tank 1).</summary>
        public float RutDepth;
    }
}
