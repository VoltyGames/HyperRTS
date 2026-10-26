using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.Selection
{
    /// <summary>The box a click must hit to pick this entity, in the entity's own space so it turns with it.</summary>
    public struct PickBox : IComponentData
    {
        public float3 Center;
        public float3 HalfSize;

        /// <summary>Where along <paramref name="ray"/> it enters this box, as a fraction of its length; 0 from inside.</summary>
        public readonly bool TryHit(in LocalTransform transform, in PickRay ray, out float fraction)
        {
            var toLocal = math.conjugate(transform.Rotation);
            var origin = math.rotate(toLocal, ray.Origin - transform.Position) / transform.Scale - Center;
            var direction = math.rotate(toLocal, ray.Direction) / transform.Scale;

            // Slab test: a zero direction component gives infinities that still order correctly.
            var inverse = math.rcp(direction);
            var near = (-HalfSize - origin) * inverse;
            var far = (HalfSize - origin) * inverse;
            var enter = math.max(math.cmax(math.min(near, far)), 0f);
            var exit = math.cmin(math.max(near, far));
            fraction = enter / ray.Length;
            return enter <= exit && enter <= ray.Length;
        }
    }
}
