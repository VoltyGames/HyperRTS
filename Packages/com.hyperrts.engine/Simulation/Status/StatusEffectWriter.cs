using Unity.Entities;

namespace HyperRTS.Simulation.Status
{
    /// <summary>Queues effects for <see cref="StatusEffectSystem"/> from one job thread without a sync point.</summary>
    public struct StatusEffectWriter
    {
        private BufferLookup<StatusRequest> _requests;
        private Entity _queue;

        public StatusEffectWriter(ref SystemState state) : this()
        {
            _requests = state.GetBufferLookup<StatusRequest>();
        }

        /// <summary>Call each update before scheduling; <see cref="StatusEffectSystem"/> creates the queue.</summary>
        public void Update(ref SystemState state, Entity queue)
        {
            _requests.Update(ref state);
            _queue = queue;
        }

        /// <summary>Applies the effect prefab <paramref name="definition"/> this frame.</summary>
        public void Apply(Entity target, Entity definition, Entity source, byte sourceFaction) =>
            _requests[_queue].Add(new StatusRequest
            {
                Target = target, Definition = definition, Source = source, SourceFaction = sourceFaction,
            });
    }
}
