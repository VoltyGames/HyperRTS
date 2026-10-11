using System.Collections.Generic;
using HyperRTS.Simulation.Combat;
using HyperRTS.Simulation.Common;
using Unity.Collections;
using Unity.Entities;

namespace HyperRTS.Presentation.Impacts
{
    /// <summary>
    /// The <see cref="ImpactMark"/>s of each shooter type, read from any prefab or instance of the type and cached: the
    /// shooter may be gone by the time its shell lands, and on a client the event names a type the server resolved.
    /// </summary>
    public sealed class ImpactMarkCatalog
    {
        private readonly Dictionary<int, ImpactMark[]> _marks = new();
        private readonly HashSet<int> _missing = new();
        private readonly EntityQuery _typed;
        private int _typedVersion;

        public ImpactMarkCatalog(EntityQuery typed) => _typed = typed;

        /// <summary>
        /// The type's marks, or null. Misses are cached too, until an entity with marks is created or destroyed (a
        /// prefab streaming in).
        /// </summary>
        public ImpactMark[] Find(EntityManager entityManager, in PrefabRegistry registry, int typeId)
        {
            if (_marks.TryGetValue(typeId, out var marks))
            {
                return marks;
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

            var entries = entityManager.GetBuffer<EntityImpactMark>(source, true);
            marks = new ImpactMark[entries.Length];
            for (var i = 0; i < marks.Length; i++)
            {
                marks[i] = entries[i].Mark.Value;
            }

            _marks[typeId] = marks;
            return marks;
        }

        /// <summary>The type's prefab, else any instance: a SubScene-placed entity may have no prefab loaded.</summary>
        private Entity FindSource(EntityManager entityManager, in PrefabRegistry registry, int typeId)
        {
            var prefab = registry.Find(typeId);
            if (prefab != Entity.Null && entityManager.HasBuffer<EntityImpactMark>(prefab))
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
    }
}
