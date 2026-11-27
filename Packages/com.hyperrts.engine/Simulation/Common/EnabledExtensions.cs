using Unity.Entities;

namespace HyperRTS.Simulation.Common;

/// <summary>"Has the component and it is enabled", for optional toggles like <c>Inside</c>.</summary>
public static class EnabledExtensions
{
    extension<T>(in ComponentLookup<T> lookup) where T : unmanaged, IComponentData, IEnableableComponent
    {
        public bool HasEnabled(Entity entity) =>
            lookup.HasComponent(entity) && lookup.IsComponentEnabled(entity);
    }

    extension(EntityManager entityManager)
    {
        public bool HasEnabled<T>(Entity entity)
            where T : unmanaged, IComponentData, IEnableableComponent =>
            entityManager.HasComponent<T>(entity) && entityManager.IsComponentEnabled<T>(entity);
    }

    extension<T>(ref ComponentLookup<T> lookup) where T : unmanaged, IComponentData, IEnableableComponent
    {
        /// <summary>Turns an optional toggle off on entities that have it.</summary>
        public void DisableIfPresent(Entity entity)
        {
            if (lookup.HasComponent(entity))
            {
                lookup.SetComponentEnabled(entity, false);
            }
        }
    }
}
