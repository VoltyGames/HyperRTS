using System;
using Unity.Entities;

namespace HyperRTS.Presentation.Common;

/// <summary>
/// Entity query for MonoBehaviours that is recreated when the world it belongs to is replaced. Built lazily:
/// component types can't be resolved while MonoBehaviours deserialize, before the TypeManager initializes.
/// </summary>
public sealed class LiveQuery(Func<EntityManager, EntityQuery> create)
{
    private World _world;
    private EntityQuery _query;

    public EntityQuery In(EntityManager entityManager)
    {
        if (entityManager.World != _world)
        {
            _world = entityManager.World;
            _query = create(entityManager);
        }

        return _query;
    }
}
