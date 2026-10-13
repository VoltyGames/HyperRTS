using HyperRTS.Simulation.Common;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;

namespace HyperRTS.Simulation.Combat
{
    /// <summary>Applies scenery states another world decided, such as a multiplayer server, to this world's map.</summary>
    public struct SceneryWriter
    {
        private ComponentLookup<Health> _health;
        private ComponentLookup<ShownDamageStage> _shownStages;
        private ComponentLookup<SpawnOnDeath> _remains;
        private ComponentLookup<SpawnEffect> _effects;
        private ComponentLookup<LocalTransform> _transforms;
        private ComponentLookup<PostTransformMatrix> _stretches;
        private ComponentLookup<Topple> _topples;
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
            _topples = state.GetComponentLookup<Topple>(true);
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
            _topples.Update(ref state);
            _groups.Update(ref state);
            _children.Update(ref state);
        }

        /// <summary>False once the piece is destroyed here or its SubScene unloaded.</summary>
        public bool Exists(Entity scenery) => _health.HasComponent(scenery);

        public void SetHealth(Entity scenery, float fraction, bool witnessed)
        {
            var health = _health[scenery];
            health.Current = health.Max * fraction;
            _health[scenery] = health;

            // Like a freshly loaded wreck, the stage model changes without its damage effect.
            if (!witnessed && _shownStages.HasComponent(scenery))
            {
                _shownStages[scenery] = new ShownDamageStage { Index = -1 };
            }
        }

        /// <summary>A <see cref="Topple"/> piece's remains lie toward <paramref name="fall"/>.</summary>
        public void Destroy(Entity scenery, bool witnessed, byte fall, EntityCommandBuffer ecb)
        {
            if (_remains.TryGetComponent(scenery, out var spawn))
            {
                SpawnRemains(scenery, spawn.Prefab, witnessed, fall, ecb);
            }

            var parts = new NativeList<Entity>(4, Allocator.Temp);
            EntityParts.Collect(scenery, _groups, _children, parts);
            ecb.DestroyEntity(parts.AsArray());
        }

        private void SpawnRemains(Entity scenery, Entity prefab, bool witnessed, byte fall, EntityCommandBuffer ecb)
        {
            var remains = Remains.Of(scenery, _transforms[scenery], fall, witnessed, _stretches, _topples);
            var spawned = ecb.Instantiate(prefab);
            ecb.SetComponent(spawned, remains.Transform);
            if (remains.Stretched)
            {
                ecb.AddComponent(spawned, remains.Stretch);
            }

            if (remains.Felled)
            {
                ecb.AddComponent(spawned, remains.Felling);
            }

            if (!witnessed && _effects.HasComponent(prefab))
            {
                ecb.SetComponentEnabled<SpawnEffect>(spawned, false);
            }
        }
    }
}
