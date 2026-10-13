using HyperRTS.Simulation.Common;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;

namespace HyperRTS.Simulation.Combat
{
    /// <summary>
    /// Shows scenery states another world decided (a multiplayer server) on this world's own copy of the map:
    /// health, so damage stages follow, or destruction, which removes the piece whole and spawns its remains here.
    /// A change nobody saw happen shows its result without the effects of getting there.
    /// </summary>
    public struct SceneryWriter
    {
        private ComponentLookup<Health> _health;
        private ComponentLookup<ShownDamageStage> _shownStages;
        private ComponentLookup<SpawnOnDeath> _remains;
        private ComponentLookup<SpawnEffect> _effects;
        private ComponentLookup<LocalTransform> _transforms;
        private ComponentLookup<PostTransformMatrix> _stretches;
        private BufferLookup<LinkedEntityGroup> _groups;
        private BufferLookup<Child> _children;

        public SceneryWriter(ref SystemState state)
        {
            _health = state.GetComponentLookup<Health>();
            _shownStages = state.GetComponentLookup<ShownDamageStage>();
            _remains = state.GetComponentLookup<SpawnOnDeath>(true);
            _effects = state.GetComponentLookup<SpawnEffect>(true);
            _transforms = state.GetComponentLookup<LocalTransform>(true);
            _stretches = state.GetComponentLookup<PostTransformMatrix>(true);
            _groups = state.GetBufferLookup<LinkedEntityGroup>(true);
            _children = state.GetBufferLookup<Child>(true);
        }

        public void Update(ref SystemState state)
        {
            _health.Update(ref state);
            _shownStages.Update(ref state);
            _remains.Update(ref state);
            _effects.Update(ref state);
            _transforms.Update(ref state);
            _stretches.Update(ref state);
            _groups.Update(ref state);
            _children.Update(ref state);
        }

        /// <summary>False once the piece is gone (destroyed here, or its SubScene unloaded).</summary>
        public bool Exists(Entity scenery) => _health.HasComponent(scenery);

        public void SetHealth(Entity scenery, float fraction, bool witnessed)
        {
            var health = _health[scenery];
            health.Current = health.Max * fraction;
            _health[scenery] = health;

            // Like a freshly loaded wreck: the stage model changes without replaying its damage effect.
            if (!witnessed && _shownStages.HasComponent(scenery))
            {
                _shownStages[scenery] = new ShownDamageStage { Index = -1 };
            }
        }

        /// <summary>Removes the piece with its parts and spawns its remains where it stood, through <paramref name="ecb"/>.</summary>
        public void Destroy(Entity scenery, bool witnessed, EntityCommandBuffer ecb)
        {
            if (_remains.TryGetComponent(scenery, out var spawn))
            {
                var remains = ecb.Instantiate(spawn.Prefab);
                ecb.SetComponent(remains, _transforms[scenery]);
                if (_stretches.TryGetComponent(scenery, out var stretch))
                {
                    ecb.AddComponent(remains, stretch);
                }

                if (!witnessed && _effects.HasComponent(spawn.Prefab))
                {
                    ecb.SetComponentEnabled<SpawnEffect>(remains, false);
                }
            }

            var parts = new NativeList<Entity>(4, Allocator.Temp);
            EntityParts.Collect(scenery, _groups, _children, parts);
            ecb.DestroyEntity(parts.AsArray());
        }
    }
}
