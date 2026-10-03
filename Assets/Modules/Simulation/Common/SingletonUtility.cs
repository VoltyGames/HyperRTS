using Unity.Entities;

namespace HyperRTS.Simulation.Common
{
    /// <summary>Gets or creates client singletons, so whichever layer (input or HUD) starts first owns the entity.</summary>
    public static class SingletonUtility
    {
        public static Entity Ensure<T>(EntityManager entityManager) where T : unmanaged, IComponentData
        {
            using var query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<T>());
            return query.TryGetSingletonEntity<T>(out var entity) ? entity : entityManager.CreateEntity(typeof(T));
        }
    }
}
