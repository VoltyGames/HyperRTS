using Unity.Entities;

namespace HyperRTS.Simulation.Units
{
    /// <summary>Ground contact of a vehicle's tracks or wheels for presentation's ruts. Nothing in the simulation reads it.</summary>
    public struct TrackMarks : IComponentData
    {
        public float Width;

        public float Gauge;

        /// <summary>Rut depth from 0 to 1, about 0.3 for a light car and 1 for a tank.</summary>
        public float RutDepth;
    }
}
