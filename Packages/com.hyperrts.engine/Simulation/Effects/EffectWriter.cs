using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Vision;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.Effects
{
    /// <summary>
    /// Appends to the <see cref="EffectQueue"/> from a job without a sync point. Entities without a rig for the slot
    /// write nothing, so plain units cost no events or bandwidth. Use from one thread.
    /// </summary>
    public struct EffectWriter
    {
        private BufferLookup<EffectEvent> _events;
        [ReadOnly] private BufferLookup<EntityEffect> _effects;
        [ReadOnly] private ComponentLookup<EntityInfo> _info;
        [ReadOnly] private ComponentLookup<Stealthed> _cloaks;
        private Entity _queue;

        public EffectWriter(ref SystemState state) : this()
        {
            _events = state.GetBufferLookup<EffectEvent>();
            _effects = state.GetBufferLookup<EntityEffect>(true);
            _info = state.GetComponentLookup<EntityInfo>(true);
            _cloaks = state.GetComponentLookup<Stealthed>(true);
        }

        /// <summary>Call each update before scheduling; the queue exists once <see cref="EffectClearSystem"/> is created.</summary>
        public void Update(ref SystemState state, Entity queue)
        {
            _events.Update(ref state);
            _effects.Update(ref state);
            _info.Update(ref state);
            _cloaks.Update(ref state);
            _queue = queue;
        }

        /// <summary>
        /// Plays <paramref name="source"/>'s rig for the slot at its socket, placed by <paramref name="transform"/>;
        /// nothing if it has none.
        /// </summary>
        public void Play(Entity source, EffectSlot slot, in LocalTransform transform, float3 direction, byte faction)
        {
            if (!_effects.TryGetBuffer(source, out var effects))
            {
                return;
            }

            if (!EffectRules.TryGetSocket(effects, slot, out var socket))
            {
                return;
            }

            if (!_info.TryGetComponent(source, out var info) || info.TypeId == 0)
            {
                return;
            }

            _events[_queue].Add(new EffectEvent
            {
                TypeId = info.TypeId, Slot = slot, Faction = faction, Position = transform.TransformPoint(socket),
                Direction = direction, Stealthed = _cloaks.HasEnabled(source),
            });
        }
    }
}
