using System.Collections.Generic;
using Unity.Entities;

namespace HyperRTS.Simulation.Common.Authoring
{
    /// <summary>Adds components to one entity through a baker or an <see cref="EntityManager"/>, so bakers and tests build identical entities.</summary>
    public readonly struct EntityWriter
    {
        private readonly IBaker _baker;
        private readonly EntityManager _entityManager;
        private readonly Entity _entity;

        internal EntityWriter(IBaker baker, Entity entity)
        {
            _baker = baker;
            _entityManager = default;
            _entity = entity;
        }

        public EntityWriter(EntityManager entityManager, Entity entity)
        {
            _baker = null;
            _entityManager = entityManager;
            _entity = entity;
        }

        public void Add<T>() where T : unmanaged, IComponentData
        {
            if (_baker != null)
            {
                _baker.AddComponent<T>(_entity);
                return;
            }

            _entityManager.AddComponent<T>(_entity);
        }

        public void Add<T>(in T component) where T : unmanaged, IComponentData
        {
            if (_baker != null)
            {
                _baker.AddComponent(_entity, component);
                return;
            }

            _entityManager.AddComponentData(_entity, component);
        }

        public DynamicBuffer<T> AddBuffer<T>() where T : unmanaged, IBufferElementData =>
            _baker != null ? _baker.AddBuffer<T>(_entity) : _entityManager.AddBuffer<T>(_entity);

        /// <summary>Adds a buffer holding <paramref name="items"/>; a null list adds it empty.</summary>
        public void AddBuffer<T>(IReadOnlyList<T> items) where T : unmanaged, IBufferElementData
        {
            var buffer = AddBuffer<T>();
            if (items == null)
            {
                return;
            }

            foreach (var item in items)
            {
                buffer.Add(item);
            }
        }

        public void SetEnabled<T>(bool enabled) where T : unmanaged, IComponentData, IEnableableComponent
        {
            if (_baker != null)
            {
                _baker.SetComponentEnabled<T>(_entity, enabled);
                return;
            }

            _entityManager.SetComponentEnabled<T>(_entity, enabled);
        }
    }

    /// <summary>Starts an <see cref="EntityWriter"/> on one of a baker's entities.</summary>
    public static class BakerExtensions
    {
        public static EntityWriter Writer(this IBaker baker, TransformUsageFlags flags = TransformUsageFlags.Dynamic) =>
            new(baker, baker.GetEntity(flags));

        public static EntityWriter Writer(this IBaker baker, Entity entity) => new(baker, entity);
    }
}
