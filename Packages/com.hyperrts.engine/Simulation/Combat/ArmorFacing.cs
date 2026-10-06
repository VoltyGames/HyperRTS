using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.Combat
{
    /// <summary>Damage multipliers by the side a hit comes from: front and rear are 90° arcs, the sides the rest.</summary>
    public struct ArmorFacing : IComponentData
    {
        /// <summary>cos 45°: hits within 45° of the facing axis count as front or rear.</summary>
        public const float ArcCosine = 0.70710678f;

        public float Front;
        public float Side;
        public float Rear;

        public readonly float Multiplier(in LocalTransform target, float3 origin)
        {
            var toOrigin = origin.xz - target.Position.xz;
            if (math.lengthsq(toOrigin) < 1e-6f)
            {
                return Front;
            }

            var cosine = math.dot(math.normalize(math.forward(target.Rotation).xz), math.normalize(toOrigin));
            return cosine >= ArcCosine ? Front : cosine <= -ArcCosine ? Rear : Side;
        }
    }
}
