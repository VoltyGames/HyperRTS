using Unity.Collections;
using Unity.Entities;

namespace HyperRTS.Simulation.Common
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
        /// Call each update before scheduling. Jobs copy the writer, so it can't hold the queue's query; the queue
        /// entity lives as long as the world, so it is found once, on the first update, since its owner may create it
        /// after this system's OnCreate. The query is the EntityManager's, not the system's, and is disposed, so the
        /// system asks for no query mid-update.
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
