using Unity.Entities;

namespace HyperRTS.Presentation.Common
{
    /// <summary>
    /// A singleton read and written from MonoBehaviours, following the world it lives in. Reads and writes of a world
    /// without it (headless) do nothing.
    /// </summary>
    public sealed class LiveSingleton<T> where T : unmanaged, IComponentData
    {
        private readonly LiveQuery _query = new(entityManager => entityManager.CreateEntityQuery(ComponentType.ReadOnly<T>()));

        public bool TryGet(EntityManager entityManager, out T value) =>
            _query.In(entityManager).TryGetSingleton(out value);

        /// <summary>Waits for jobs writing it, before a main-thread read of data they fill.</summary>
        public void Complete(EntityManager entityManager) => _query.In(entityManager).CompleteDependency();

        public bool TryGetEntity(EntityManager entityManager, out Entity entity) =>
            _query.In(entityManager).TryGetSingletonEntity<T>(out entity);

        public void Set(EntityManager entityManager, in T value)
        {
            if (TryGetEntity(entityManager, out var entity))
            {
                entityManager.SetComponentData(entity, value);
            }
        }
    }
}
