using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.Common
{
    /// <summary>
    /// Spawns and places roots. Command buffers play back after the transform systems, so a placed root also gets
    /// the <see cref="LocalToWorld"/> they would compute, or its first drawn frame shows the prefab's pose.
    /// </summary>
    public static class SpawnExtensions
    {
        /// <summary>
        /// Instantiates a baked prefab at a pose, keeping its scale and <see cref="PostTransformMatrix"/>, owned by
        /// <paramref name="faction"/>. Jobs spawn through <see cref="PrefabSpawner"/>.
        /// </summary>
        public static Entity Spawn(this EntityManager entityManager, Entity prefab, float3 position,
            quaternion rotation, byte faction)
        {
            var transform = entityManager.GetComponentData<LocalTransform>(prefab);
            transform.Position = position;
            transform.Rotation = rotation;
            var stretch = entityManager.HasComponent<PostTransformMatrix>(prefab)
                ? entityManager.GetComponentData<PostTransformMatrix>(prefab).Value
                : float4x4.identity;
            var spawned = entityManager.Instantiate(prefab);
            entityManager.AddComponentData(spawned, transform);
            entityManager.AddComponentData(spawned, World(transform, stretch));
            entityManager.AddComponentData(spawned, new Faction { Value = faction });
            return spawned;
        }

        /// <summary><paramref name="stretch"/> is the root's <see cref="PostTransformMatrix"/>.</summary>
        public static void Place(this EntityCommandBuffer ecb, Entity root, in LocalTransform transform,
            in float4x4 stretch)
        {
            ecb.AddComponent(root, transform);
            ecb.AddComponent(root, World(transform, stretch));
        }

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
