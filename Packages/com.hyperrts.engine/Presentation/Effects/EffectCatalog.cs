using System.Collections.Generic;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Effects;
using Unity.Collections;
using Unity.Entities;

namespace HyperRTS.Presentation.Effects
{
    /// <summary>
    /// Each entity type's <see cref="EffectSet"/>, cached from its prefab or any instance, since the source may be gone
    /// when its event plays.
    /// </summary>
    public sealed class EffectCatalog
    {
        private readonly Dictionary<int, EffectSet> _sets = new();
        private readonly HashSet<int> _missing = new();
        private readonly EntityQuery _typed;
        private int _typedVersion;

        /// <summary>
        /// Over entities with <see cref="EntityInfo"/> and <see cref="EntityEffect"/>s or
        /// <see cref="EntityImpactEffects"/>, prefabs included.
        /// </summary>
        public EffectCatalog(EntityQuery typed) => _typed = typed;

        /// <summary>
        /// The type's effects, or null. Misses are cached too, until an entity with effects is created or destroyed
        /// (a prefab streaming in).
        /// </summary>
        public EffectSet Find(EntityManager entityManager, in PrefabRegistry registry, int typeId)
        {
            if (_sets.TryGetValue(typeId, out var set))
            {
                return set;
            }

            var version = _typed.GetCombinedComponentOrderVersion(includeEntityType: true);
            if (version != _typedVersion)
            {
                _typedVersion = version;
                _missing.Clear();
            }

            var source = _missing.Contains(typeId) ? Entity.Null : FindSource(entityManager, registry, typeId);
            if (source == Entity.Null)
            {
                _missing.Add(typeId);
                return null;
            }

            return _sets[typeId] = Read(entityManager, source);
        }

        private static EffectSet Read(EntityManager entityManager, Entity source)
        {
            var effects = entityManager.HasBuffer<EntityEffect>(source)
                ? entityManager.GetBuffer<EntityEffect>(source, true).AsNativeArray().ToArray()
                : new EntityEffect[0];
            var impacts = entityManager.HasComponent<EntityImpactEffects>(source)
                ? entityManager.GetComponentData<EntityImpactEffects>(source).Effects.Value
                : null;
            return new EffectSet(effects, impacts);
        }

        /// <summary>The type's prefab, else any instance: a SubScene-placed entity may have no prefab loaded.</summary>
        private Entity FindSource(EntityManager entityManager, in PrefabRegistry registry, int typeId)
        {
            var prefab = registry.Find(typeId);
            if (prefab != Entity.Null && HasEffects(entityManager, prefab))
            {
                return prefab;
            }

            using var entities = _typed.ToEntityArray(Allocator.Temp);
            foreach (var entity in entities)
            {
                if (entityManager.GetComponentData<EntityInfo>(entity).TypeId == typeId)
                {
                    return entity;
                }
            }

            return Entity.Null;
        }

        private static bool HasEffects(EntityManager entityManager, Entity entity)
        {
            if (entityManager.HasBuffer<EntityEffect>(entity))
            {
                return true;
            }

            return entityManager.HasComponent<EntityImpactEffects>(entity);
        }
    }
}
