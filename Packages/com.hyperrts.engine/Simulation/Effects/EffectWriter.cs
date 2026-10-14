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
        /// false if it has none.
        /// </summary>
        public bool Play(Entity source, EffectSlot slot, in LocalTransform transform, float3 direction, byte faction)
        {
            if (!_effects.TryGetBuffer(source, out var effects))
            {
                return false;
            }

            if (!EffectRules.TryGetSocket(effects, slot, out var socket))
            {
                return false;
            }

            var typeId = _info.TryGetComponent(source, out var info) ? info.TypeId : 0;
            return Add(typeId, slot, transform.TransformPoint(socket), direction, faction, _cloaks.HasEnabled(source));
        }

        /// <summary><paramref name="source"/>'s type id if it has a rig for the slot, else 0.</summary>
        public int TypeIdFor(Entity source, EffectSlot slot)
        {
            if (!_effects.TryGetBuffer(source, out var effects) || !EffectRules.TryGetSocket(effects, slot, out _))
            {
                return 0;
            }

            return _info.TryGetComponent(source, out var info) ? info.TypeId : 0;
        }

        /// <summary>Plays the slot's rig of an entity type; a type id of 0 is ignored and returns false.</summary>
        public bool Add(int typeId, EffectSlot slot, float3 position, float3 direction, byte faction,
            bool stealthed = false)
        {
            if (typeId == 0)
            {
                return false;
            }

            _events[_queue].Add(new EffectEvent
            {
                TypeId = typeId, Slot = slot, Faction = faction, Position = position, Direction = direction,
                Stealthed = stealthed,
            });
            return true;
        }
    }
}
