using Unity.Entities;

namespace HyperRTS.Input
{
    /// <summary>Creates client singletons on demand, so whichever layer starts first owns the entity.</summary>
    public static class SingletonUtility
    {
        public static void Ensure<T>(EntityManager entityManager) where T : unmanaged, IComponentData
        {
            using var query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<T>());
            if (query.IsEmptyIgnoreFilter)
            {
                entityManager.CreateEntity(typeof(T));
            }
        }
    }
}
