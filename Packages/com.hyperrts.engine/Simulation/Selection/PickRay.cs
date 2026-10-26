using Unity.Mathematics;

namespace HyperRTS.Simulation.Selection
{
    /// <summary>A world-space click ray tested against every <see cref="PickBox"/>.</summary>
    public readonly struct PickRay
    {
        public readonly float3 Origin;

        /// <summary>Unit length, so a hit's distance along the ray is in world units.</summary>
        public readonly float3 Direction;

        public readonly float Length;

        public PickRay(float3 origin, float3 direction, float length)
        {
            Origin = origin;
            Direction = math.normalize(direction);
            Length = length;
        }

        public float3 Point(float fraction) => Origin + Direction * (fraction * Length);
    }
}
