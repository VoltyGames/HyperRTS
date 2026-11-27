using HyperRTS.Simulation.Common.Players;
using HyperRTS.Simulation.Common.Prefabs;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.Common;

/// <summary>
/// Spawns and places roots. Command buffers play back after the transform systems, so a placed root also gets
/// the <see cref="LocalToWorld"/> they would compute, or its first drawn frame shows the prefab's pose.
/// </summary>
public static class SpawnExtensions
{
    extension(EntityManager entityManager)
    {
        /// <summary>
        /// Instantiates a baked prefab at a pose, keeping its scale and <see cref="PostTransformMatrix"/>, owned by
        /// <paramref name="owner"/>. Jobs spawn through <see cref="PrefabSpawner"/>.
        /// </summary>
        public Entity Spawn(Entity prefab, float3 position,
            quaternion rotation, byte owner)
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
            entityManager.AddComponentData(spawned, new Owner { Value = owner });
            return spawned;
        }
    }

    extension(EntityCommandBuffer ecb)
    {
        /// <summary><paramref name="stretch"/> is the root's <see cref="PostTransformMatrix"/>.</summary>
        public void Place(Entity root, in LocalTransform transform,
            in float4x4 stretch)
        {
            ecb.AddComponent(root, transform);
            ecb.AddComponent(root, World(transform, stretch));
        }
    }

    extension(EntityCommandBuffer.ParallelWriter ecb)
    {
        public void Place(int sortKey, Entity root,
            in LocalTransform transform, in float4x4 stretch)
        {
            ecb.AddComponent(sortKey, root, transform);
            ecb.AddComponent(sortKey, root, World(transform, stretch));
        }
    }

    private static LocalToWorld World(in LocalTransform transform, in float4x4 stretch) =>
        new() { Value = math.mul(transform.ToMatrix(), stretch) };
}
