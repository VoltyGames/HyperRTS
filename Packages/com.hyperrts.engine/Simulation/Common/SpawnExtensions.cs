using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.Common
{
    /// <summary>
    /// Places a root spawned through a command buffer. Playback lands after the transform systems, so the root also
    /// gets the <see cref="LocalToWorld"/> they would compute, or its first drawn frame shows the prefab's pose.
    /// </summary>
    public static class SpawnExtensions
    {
        public static void Place(this EntityCommandBuffer ecb, Entity root, in LocalTransform transform) =>
            ecb.Place(root, transform, float4x4.identity);

        /// <summary><paramref name="stretch"/> is the root's <see cref="PostTransformMatrix"/>.</summary>
        public static void Place(this EntityCommandBuffer ecb, Entity root, in LocalTransform transform,
            in float4x4 stretch)
        {
            ecb.AddComponent(root, transform);
            ecb.AddComponent(root, World(transform, stretch));
        }

        public static void Place(this EntityCommandBuffer.ParallelWriter ecb, int sortKey, Entity root,
            in LocalTransform transform) => ecb.Place(sortKey, root, transform, float4x4.identity);

        public static void Place(this EntityCommandBuffer.ParallelWriter ecb, int sortKey, Entity root,
            in LocalTransform transform, in float4x4 stretch)
        {
            ecb.AddComponent(sortKey, root, transform);
            ecb.AddComponent(sortKey, root, World(transform, stretch));
        }

        private static LocalToWorld World(in LocalTransform transform, in float4x4 stretch) =>
            new() { Value = math.mul(transform.ToMatrix(), stretch) };
    }
}
