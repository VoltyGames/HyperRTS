using System;
using HyperRTS.Presentation.Common;
using Unity.Collections;
using Unity.Entities;

namespace HyperRTS.Presentation.HUD
{
    /// <summary>Tells a widget whether any watched component was written, or an entity count moved, since the last look.</summary>
    public sealed class ChangeWatch
    {
        private readonly Func<ComponentType[]> _types;
        private readonly LiveQuery _query;
        private ComponentType[] _resolved;
        private World _world;
        private uint _lastVersion;
        private int _count;

        /// <param name="types">Resolved lazily: component types can't be read before the TypeManager initializes.</param>
        public ChangeWatch(Func<ComponentType[]> types)
        {
            _types = types;
            _query = new LiveQuery(entityManager => entityManager.CreateEntityQuery(new EntityQueryDesc
            {
                Any = Types,
                Options = EntityQueryOptions.IncludeDisabledEntities,
            }));
        }

        private ComponentType[] Types => _resolved ??= _types();

        /// <summary>True on the first look in a world and whenever something watched changed since the last one.</summary>
        public bool Moved(EntityManager entityManager)
        {
            var first = entityManager.World != _world;
            var query = _query.In(entityManager);
            var count = query.CalculateEntityCountWithoutFiltering();
            var moved = first || count != _count || AnyWritten(entityManager, query);
            _world = entityManager.World;
            _count = count;
            _lastVersion = entityManager.GlobalSystemVersion;
            return moved;
        }

        /// <summary>Makes the next look report a change, for when the widget dropped what it read.</summary>
        public void Forget() => _world = null;

        // A write later in the version already read counts again next look, so none is missed.
        private bool AnyWritten(EntityManager entityManager, EntityQuery query)
        {
            var since = _lastVersion - 1;
            using var chunks = query.ToArchetypeChunkArray(Allocator.Temp);
            foreach (var type in Types)
            {
                var handle = entityManager.GetDynamicComponentTypeHandle(type);
                foreach (var chunk in chunks)
                {
                    if (chunk.Has(ref handle) && chunk.DidChange(ref handle, since))
                    {
                        return true;
                    }
                }
            }

            return false;
        }
    }
}
