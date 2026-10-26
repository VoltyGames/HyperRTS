using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Selection
{
    /// <summary>An entity whose <see cref="PickBox"/> a <see cref="PickRay"/> entered, and where.</summary>
    public struct PickHit
    {
        public Entity Entity;
        public float3 Point;

        /// <summary>How far along the ray the box starts, 0 to 1; the smallest is the closest hit.</summary>
        public float Fraction;
    }
}
