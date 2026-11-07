using Unity.Collections;
using Unity.Entities;

namespace HyperRTS.Simulation.Common.Events
{
    /// <summary>
    /// Appends to the queue singleton holding a <typeparamref name="T"/> buffer, from a job without a sync point.
    /// Use from one thread, so events keep their order.
    /// </summary>
    public struct EventWriter<T> where T : unmanaged, IBufferElementData
    {
        private BufferLookup<T> _events;
        private Entity _queue;

        public EventWriter(ref SystemState state)
        {
            _events = state.GetBufferLookup<T>();
            _queue = Entity.Null;
            state.RequireForUpdate<T>();
        }

        /// <summary>
        /// Call each update before scheduling. Jobs copy the writer, so the queue entity is found once, on the first
        /// update (its owner may create it after this system's OnCreate); the system asks for no query mid-update
        /// because the query is the EntityManager's and is disposed.
        /// </summary>
        public void Update(ref SystemState state)
        {
            _events.Update(ref state);
            if (_queue == Entity.Null)
            {
                using var queue = new EntityQueryBuilder(Allocator.Temp).WithAll<T>().Build(state.EntityManager);
                _queue = queue.GetSingletonEntity();
            }
        }

        public void Add(in T item) => _events[_queue].Add(item);
    }
}
