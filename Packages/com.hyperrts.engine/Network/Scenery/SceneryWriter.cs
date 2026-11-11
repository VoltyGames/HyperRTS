using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Common.Combat;
using HyperRTS.Simulation.Death;
using HyperRTS.Simulation.Destruction;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;

namespace HyperRTS.Network.Scenery
{
    /// <summary>Applies scenery states another world decided, such as a multiplayer server, to this world's map.</summary>
    public struct SceneryWriter
    {
        private ComponentLookup<Health> _health;
        private ComponentLookup<ShownDamageStage> _shownStages;
        private ComponentLookup<ShownSections> _shownSections;
        private ComponentLookup<SectionedBuilding> _buildings;
        private BufferLookup<Section> _sections;
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
            _shownSections = state.GetComponentLookup<ShownSections>();
            _buildings = state.GetComponentLookup<SectionedBuilding>();
            _sections = state.GetBufferLookup<Section>();
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
            _shownSections.Update(ref state);
            _buildings.Update(ref state);
            _sections.Update(ref state);
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

            // Like a freshly loaded wreck, the stage model and broken sections change without their effects.
            if (!witnessed && _shownStages.HasComponent(scenery))
            {
                _shownStages[scenery] = new ShownDamageStage { Index = -1 };
            }

            if (!witnessed && _shownSections.HasComponent(scenery))
            {
                var shown = _shownSections[scenery];
                shown.Seen = false;
                _shownSections[scenery] = shown;
            }
        }

        /// <summary>Matches a sectioned building's broken sections to <paramref name="broken"/>, as the server's are.</summary>
        public void SetBroken(Entity scenery, ulong broken)
        {
            if (!_buildings.TryGetComponent(scenery, out var building))
            {
                return;
            }

            building.SetBroken(_sections[scenery], broken);
            _buildings[scenery] = building;
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
            EntityParts.Destroy(piece, _groups, _children, ecb.AsParallelWriter(), 0);
        }

        private void SpawnRemains(Entity scenery, Entity prefab, bool witnessed, byte fall, EntityCommandBuffer ecb)
        {
            var remains = Remains.FromPiece(scenery, _transforms[scenery], fall, witnessed, _remainsLookups);
            var spawned = remains.Spawn(ecb.AsParallelWriter(), 0, prefab);
            if (!witnessed && _effects.HasComponent(prefab))
            {
                ecb.SetComponentEnabled<SpawnEffect>(spawned, false);
            }

            ecb.AddComponent(spawned, _ids[scenery]);
        }
    }
}
