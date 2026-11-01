using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Common.Authoring;
using Unity.Entities;

namespace HyperRTS.Simulation.Tests
{
    /// <summary>Reads and writes of components on the test world's entities.</summary>
    public sealed partial class TestWorld
    {
        public T Get<T>(Entity entity) where T : unmanaged, IComponentData => EntityManager.GetComponentData<T>(entity);

        public T Singleton<T>() where T : unmanaged, IComponentData
        {
            using var query = EntityManager.CreateEntityQuery(typeof(T));
            return query.GetSingleton<T>();
        }

        /// <summary>The match singleton carrying <see cref="MapSettings"/>.</summary>
        public Entity MatchEntity()
        {
            using var query = EntityManager.CreateEntityQuery(typeof(MapSettings));
            return query.GetSingletonEntity();
        }

        public bool IsEnabled<T>(Entity entity) where T : unmanaged, IComponentData, IEnableableComponent =>
            EntityManager.IsComponentEnabled<T>(entity);

        public void Set<T>(Entity entity, T value) where T : unmanaged, IComponentData =>
            EntityManager.SetComponentData(entity, value);

        public void Add<T>(Entity entity, T value) where T : unmanaged, IComponentData =>
            EntityManager.AddComponentData(entity, value);

        public DynamicBuffer<T> Buffer<T>(Entity entity) where T : unmanaged, IBufferElementData =>
            EntityManager.GetBuffer<T>(entity);

        public void SetEnabled<T>(Entity entity, bool enabled) where T : unmanaged, IComponentData, IEnableableComponent =>
            EntityManager.SetComponentEnabled<T>(entity, enabled);

        public EntityWriter Writer(Entity entity) => new(EntityManager, entity);

        /// <summary>A query over <paramref name="types"/>; the caller disposes it.</summary>
        public EntityQuery Query(params ComponentType[] types) => EntityManager.CreateEntityQuery(types);
    }
}
