using Unity.Entities;

namespace HyperRTS.Simulation.Common
{
    /// <summary>"Has the component and it is enabled", for optional toggles like <c>Inside</c>.</summary>
    public static class EnabledExtensions
    {
        public static bool HasEnabled<T>(this in ComponentLookup<T> lookup, Entity entity)
            where T : unmanaged, IComponentData, IEnableableComponent =>
            lookup.HasComponent(entity) && lookup.IsComponentEnabled(entity);

        public static bool HasEnabled<T>(this EntityManager entityManager, Entity entity)
            where T : unmanaged, IComponentData, IEnableableComponent =>
            entityManager.HasComponent<T>(entity) && entityManager.IsComponentEnabled<T>(entity);

        /// <summary>Turns an optional toggle off on entities that have it.</summary>
        public static void DisableIfPresent<T>(this ref ComponentLookup<T> lookup, Entity entity)
            where T : unmanaged, IComponentData, IEnableableComponent
        {
            if (lookup.HasComponent(entity))
            {
                lookup.SetComponentEnabled(entity, false);
            }
        }
    }
}
