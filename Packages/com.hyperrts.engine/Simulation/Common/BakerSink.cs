using Unity.Entities;

namespace HyperRTS.Simulation.Common
{
    /// <summary>Writes setup helpers into a baker.</summary>
    public readonly struct BakerSink : IComponentSink
    {
        private readonly IBaker _baker;
        private readonly Entity _entity;

        public BakerSink(IBaker baker, Entity entity)
        {
            _baker = baker;
            _entity = entity;
        }

        public void Add<T>() where T : unmanaged, IComponentData => _baker.AddComponent<T>(_entity);

        public void Add<T>(in T component) where T : unmanaged, IComponentData =>
            _baker.AddComponent(_entity, component);

        public DynamicBuffer<T> AddBuffer<T>() where T : unmanaged, IBufferElementData =>
            _baker.AddBuffer<T>(_entity);

        public void SetEnabled<T>(bool enabled) where T : unmanaged, IComponentData, IEnableableComponent =>
            _baker.SetComponentEnabled<T>(_entity, enabled);
    }
}
