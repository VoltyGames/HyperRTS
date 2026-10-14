using System;
using System.Collections.Generic;
using HyperRTS.Simulation.Common;
using Unity.Collections;
using Unity.Entities;

namespace HyperRTS.Presentation.Common
{
    /// <summary>
    /// Per entity type presentation data, read from the type's prefab or any instance and cached, since the source may
    /// be gone when its event plays and on a client the event names a type the server resolved.
    /// </summary>
    public sealed class TypeCatalog<T> where T : class
    {
        private readonly Dictionary<int, T> _entries = new();
        private readonly HashSet<int> _missing = new();
        private readonly EntityQuery _typed;
        private readonly Func<EntityManager, Entity, T> _read;
        private int _typedVersion;

        /// <summary>
        /// <paramref name="typed"/> is over entities with <see cref="EntityInfo"/> and the data, prefabs included: an
        /// entity it matches has data. <paramref name="read"/> builds the entry from such an entity.
        /// </summary>
        public TypeCatalog(EntityQuery typed, Func<EntityManager, Entity, T> read)
        {
            _typed = typed;
            _read = read;
        }

        /// <summary>
        /// The type's entry, or null. A type with a loaded prefab is cached for good; one found only by its instances
        /// misses until an entity with data is created or destroyed (a prefab streaming in).
        /// </summary>
        public T Find(EntityManager entityManager, in PrefabRegistry registry, int typeId)
        {
            if (_entries.TryGetValue(typeId, out var entry))
            {
                return entry;
            }

            var prefab = registry.Find(typeId);
            if (prefab != Entity.Null)
            {
                return _entries[typeId] = _typed.Matches(prefab) ? _read(entityManager, prefab) : null;
            }

            var version = _typed.GetCombinedComponentOrderVersion(includeEntityType: true);
            if (version != _typedVersion)
            {
                _typedVersion = version;
                _missing.Clear();
            }

            var source = _missing.Contains(typeId) ? Entity.Null : FindInstance(entityManager, typeId);
            if (source == Entity.Null)
            {
                _missing.Add(typeId);
                return null;
            }

            return _entries[typeId] = _read(entityManager, source);
        }

        /// <summary>Any instance of the type: a SubScene-placed entity may have no prefab loaded.</summary>
        private Entity FindInstance(EntityManager entityManager, int typeId)
        {
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
