using Unity.Entities;

namespace HyperRTS.Simulation.Common
{
    /// <summary>Writes setup helpers straight into an <see cref="EntityManager"/> (tests, tools).</summary>
    public readonly struct EntityManagerSink : IComponentSink
    {
        private readonly EntityManager _entityManager;
        private readonly Entity _entity;

        public EntityManagerSink(EntityManager entityManager, Entity entity)
        {
            _entityManager = entityManager;
            _entity = entity;
        }

        public void Add<T>() where T : unmanaged, IComponentData => _entityManager.AddComponent<T>(_entity);

        public void Add<T>(in T component) where T : unmanaged, IComponentData =>
            _entityManager.AddComponentData(_entity, component);

        public DynamicBuffer<T> AddBuffer<T>() where T : unmanaged, IBufferElementData =>
            _entityManager.AddBuffer<T>(_entity);

        public void SetEnabled<T>(bool enabled) where T : unmanaged, IComponentData, IEnableableComponent =>
            _entityManager.SetComponentEnabled<T>(_entity, enabled);
    }
}
