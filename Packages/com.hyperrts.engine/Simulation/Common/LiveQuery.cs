using System;
using Unity.Entities;

namespace HyperRTS.Simulation.Common
{
    /// <summary>
    /// Entity query for MonoBehaviours that is recreated when the world it belongs to is replaced. Built lazily:
    /// component types can't be resolved while MonoBehaviours deserialize, before the TypeManager initializes.
    /// </summary>
    public sealed class LiveQuery
    {
        private readonly Func<EntityManager, EntityQuery> _create;
        private World _world;
        private EntityQuery _query;

        public LiveQuery(Func<EntityManager, EntityQuery> create)
        {
            _create = create;
        }

        public EntityQuery In(EntityManager entityManager)
        {
            if (entityManager.World != _world)
            {
                _world = entityManager.World;
                _query = _create(entityManager);
            }

            return _query;
        }
    }
}
