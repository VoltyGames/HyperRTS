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
        /// <paramref name="faction"/>.
        /// </summary>
        public static Entity Spawn(this EntityCommandBuffer ecb, Entity prefab, in LocalTransform prefabTransform,
            in ComponentLookup<PostTransformMatrix> stretches, float3 position, quaternion rotation, byte faction)
        {
            var spawned = ecb.Instantiate(prefab);
            ecb.Place(spawned, Posed(prefabTransform, position, rotation), Stretch(stretches, prefab));
            // Add rather than Set: unit prefabs bake a Faction, projectile and ability prefabs don't.
            ecb.AddComponent(spawned, new Faction { Value = faction });
            return spawned;
        }

        public static Entity Spawn(this EntityCommandBuffer.ParallelWriter ecb, int sortKey, Entity prefab,
            in LocalTransform prefabTransform, in ComponentLookup<PostTransformMatrix> stretches, float3 position,
            quaternion rotation, byte faction)
        {
            var spawned = ecb.Instantiate(sortKey, prefab);
            ecb.Place(sortKey, spawned, Posed(prefabTransform, position, rotation), Stretch(stretches, prefab));
            ecb.AddComponent(sortKey, spawned, new Faction { Value = faction });
            return spawned;
        }

        public static Entity Spawn(this EntityManager entityManager, Entity prefab, float3 position,
            quaternion rotation, byte faction)
        {
            var transform = Posed(entityManager.GetComponentData<LocalTransform>(prefab), position, rotation);
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

        private static LocalTransform Posed(LocalTransform prefabTransform, float3 position, quaternion rotation)
        {
            prefabTransform.Position = position;
            prefabTransform.Rotation = rotation;
            return prefabTransform;
        }

        private static float4x4 Stretch(ComponentLookup<PostTransformMatrix> stretches, Entity prefab) =>
            stretches.TryGetComponent(prefab, out var stretch) ? stretch.Value : float4x4.identity;

        private static LocalToWorld World(in LocalTransform transform, in float4x4 stretch) =>
            new() { Value = math.mul(transform.ToMatrix(), stretch) };
    }
}
