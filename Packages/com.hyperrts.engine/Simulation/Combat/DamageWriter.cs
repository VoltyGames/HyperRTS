using Unity.Entities;

namespace HyperRTS.Simulation.Combat
{
    /// <summary>Appends to the <see cref="DamageQueue"/> from a job without a sync point. Use from one thread.</summary>
    public struct DamageWriter
    {
        private BufferLookup<DamageEvent> _events;
        private Entity _queue;

        public DamageWriter(ref SystemState state) : this()
        {
            _events = state.GetBufferLookup<DamageEvent>();
        }

        /// <summary>Call each update before scheduling; the queue exists once <see cref="DamageSystem"/> is created.</summary>
        public void Update(ref SystemState state, Entity queue)
        {
            _events.Update(ref state);
            _queue = queue;
        }

        public void Add(in DamageEvent hit) => _events[_queue].Add(hit);
    }
}
