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
        private ComponentLookup<ShownBreakage> _shownBreakage;
        private ComponentLookup<SpawnOnDeath> _remains;
        private ComponentLookup<SpawnEffect> _effects;
        private ComponentLookup<LocalTransform> _transforms;
        private ComponentLookup<SceneryId> _ids;
        private RemainsLookups _remainsLookups;
        private BufferLookup<LinkedEntityGroup> _groups;
        private BufferLookup<Child> _children;

        public SceneryWriter(ref SystemState state)
        {
            _health = state.GetComponentLookup<Health>();
            _shownStages = state.GetComponentLookup<ShownDamageStage>();
            _shownBreakage = state.GetComponentLookup<ShownBreakage>();
            _remains = state.GetComponentLookup<SpawnOnDeath>(true);
            _effects = state.GetComponentLookup<SpawnEffect>(true);
            _transforms = state.GetComponentLookup<LocalTransform>(true);
            _ids = state.GetComponentLookup<SceneryId>(true);
            _remainsLookups = RemainsLookups.Create(ref state);
            _groups = state.GetBufferLookup<LinkedEntityGroup>(true);
            _children = state.GetBufferLookup<Child>(true);
        }

        public void Update(ref SystemState state)
        {
            _health.Update(ref state);
            _shownStages.Update(ref state);
            _shownBreakage.Update(ref state);
            _remains.Update(ref state);
            _effects.Update(ref state);
            _transforms.Update(ref state);
            _ids.Update(ref state);
            _remainsLookups.Update(ref state);
            _groups.Update(ref state);
            _children.Update(ref state);
        }

        /// <summary>False once the piece is destroyed here or its SubScene unloaded.</summary>
        public bool Exists(Entity scenery) => _health.HasComponent(scenery);

        /// <summary>The piece standing or its remains, false once either is gone here.</summary>
        public bool IsPlaced(Entity piece) => _transforms.HasComponent(piece);

        public void SetHealth(Entity scenery, float fraction, bool witnessed)
        {
            var health = _health[scenery];
            health.Current = health.Max * fraction;
            _health[scenery] = health;

            // Like a freshly loaded wreck, the stage model and broken parts change without their effects.
            if (!witnessed && _shownStages.HasComponent(scenery))
            {
                _shownStages[scenery] = new ShownDamageStage { Index = -1 };
            }

            if (!witnessed && _shownBreakage.HasComponent(scenery))
            {
                var breakage = _shownBreakage[scenery];
                breakage.Seen = false;
                _shownBreakage[scenery] = breakage;
            }
        }

        /// <summary>A <see cref="Topple"/> piece's remains lie toward <paramref name="fall"/>.</summary>
        public void Destroy(Entity scenery, bool witnessed, byte fall, EntityCommandBuffer ecb)
        {
            if (_remains.TryGetComponent(scenery, out var spawn))
            {
                SpawnRemains(scenery, spawn.Prefab, witnessed, fall, ecb);
            }

            Clear(scenery, ecb);
        }

        /// <summary>Removes a piece or its remains with nothing left behind, as a building site does.</summary>
        public void Clear(Entity piece, EntityCommandBuffer ecb)
        {
            var parts = new NativeList<Entity>(4, Allocator.Temp);
            EntityParts.Collect(piece, _groups, _children, parts);
            ecb.DestroyEntity(parts.AsArray());
        }

        private void SpawnRemains(Entity scenery, Entity prefab, bool witnessed, byte fall, EntityCommandBuffer ecb)
        {
            var remains = Remains.FromPiece(scenery, _transforms[scenery], fall, witnessed, _remainsLookups);
            var spawned = ecb.Instantiate(prefab);
            ecb.Place(spawned, remains.Transform, remains.StretchMatrix);
            if (remains.Stretched)
            {
                ecb.AddComponent(spawned, remains.Stretch);
            }

            if (remains.PartsStretched)
            {
                ecb.AddComponent(spawned, remains.Parts);
            }

            if (remains.Felled)
            {
                ecb.AddComponent(spawned, remains.ToppleFall);
            }

            if (!witnessed && _effects.HasComponent(prefab))
            {
                ecb.SetComponentEnabled<SpawnEffect>(spawned, false);
            }

            ecb.AddComponent(spawned, _ids[scenery]);
        }
    }
}
