using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.Common
{
    /// <summary>
    /// Instantiates baked prefabs at a pose from a job, keeping each prefab's scale and
    /// <see cref="PostTransformMatrix"/>, owned by a faction. Read-only, so it copies into any job.
    /// </summary>
    public struct PrefabSpawner
    {
        [ReadOnly] private ComponentLookup<LocalTransform> _transforms;
        [ReadOnly] private ComponentLookup<PostTransformMatrix> _stretches;

        public PrefabSpawner(ref SystemState state)
        {
            _transforms = state.GetComponentLookup<LocalTransform>(true);
            _stretches = state.GetComponentLookup<PostTransformMatrix>(true);
        }

        public void Update(ref SystemState state)
        {
            _transforms.Update(ref state);
            _stretches.Update(ref state);
        }

        public readonly Entity Spawn(EntityCommandBuffer ecb, Entity prefab, float3 position, quaternion rotation,
            byte faction)
        {
            var spawned = ecb.Instantiate(prefab);
            ecb.Place(spawned, Posed(prefab, position, rotation), Stretch(prefab));
            // Add rather than Set: unit prefabs bake a Faction, projectile and ability prefabs don't.
            ecb.AddComponent(spawned, new Faction { Value = faction });
            return spawned;
        }

        /// <summary>Keeps the prefab's own rotation: only the position comes from the caller.</summary>
        public readonly Entity Spawn(EntityCommandBuffer ecb, Entity prefab, float3 position, byte faction) =>
            Spawn(ecb, prefab, position, _transforms[prefab].Rotation, faction);

        public readonly Entity Spawn(EntityCommandBuffer.ParallelWriter ecb, int sortKey, Entity prefab,
            float3 position, quaternion rotation, byte faction)
        {
            var spawned = ecb.Instantiate(sortKey, prefab);
            ecb.Place(sortKey, spawned, Posed(prefab, position, rotation), Stretch(prefab));
            ecb.AddComponent(sortKey, spawned, new Faction { Value = faction });
            return spawned;
        }

        private readonly LocalTransform Posed(Entity prefab, float3 position, quaternion rotation)
        {
            var transform = _transforms[prefab];
            transform.Position = position;
            transform.Rotation = rotation;
            return transform;
        }

        private readonly float4x4 Stretch(Entity prefab) =>
            _stretches.TryGetComponent(prefab, out var stretch) ? stretch.Value : float4x4.identity;
    }
}
