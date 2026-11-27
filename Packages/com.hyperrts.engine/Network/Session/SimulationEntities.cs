using Unity.Collections;
using Unity.Entities;

namespace HyperRTS.Network.Session;

/// <summary>The query for every entity of a world except the ones Unity gives GameObjects, which go with them.</summary>
internal static class SimulationEntities
{
    /// <summary>UniversalQuery without GameObject entities: prefabs and disabled entities included.</summary>
    public static EntityQuery Query(EntityManager entityManager) =>
        new EntityQueryBuilder(Allocator.Temp)
            .WithNone<TransformRef>()
            .WithOptions(EntityQueryOptions.IncludePrefab | EntityQueryOptions.IncludeDisabledEntities)
            .Build(entityManager);
}
