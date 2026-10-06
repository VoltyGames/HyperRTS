using HyperRTS.Simulation.Common;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Audio
{
    /// <summary>
    /// Appends to the <see cref="SoundQueue"/> from a job without a sync point. Entities without a cue for the slot
    /// write nothing, so silent units cost no events or bandwidth. Use from one thread.
    /// </summary>
    public struct SoundWriter
    {
        private BufferLookup<SoundEvent> _events;
        [ReadOnly] private BufferLookup<EntitySound> _sounds;
        [ReadOnly] private ComponentLookup<EntityInfo> _info;
        private Entity _queue;

        public SoundWriter(ref SystemState state) : this()
        {
            _events = state.GetBufferLookup<SoundEvent>();
            _sounds = state.GetBufferLookup<EntitySound>(true);
            _info = state.GetComponentLookup<EntityInfo>(true);
        }

        /// <summary>Call each update before scheduling; the queue exists once <see cref="SoundClearSystem"/> is created.</summary>
        public void Update(ref SystemState state, Entity queue)
        {
            _events.Update(ref state);
            _sounds.Update(ref state);
            _info.Update(ref state);
            _queue = queue;
        }

        /// <summary>Plays <paramref name="source"/>'s cue for the slot (prefab or instance) at a position.</summary>
        public void Play(Entity source, SoundSlot slot, float3 position, byte faction) =>
            Add(TypeIdFor(source, slot), slot, position, faction);

        /// <summary><paramref name="source"/>'s type id if it has a cue for the slot, else 0.</summary>
        public int TypeIdFor(Entity source, SoundSlot slot)
        {
            if (!_sounds.TryGetBuffer(source, out var sounds) || !SoundRules.TryGetCue(sounds, slot, out _))
            {
                return 0;
            }

            return _info.TryGetComponent(source, out var info) ? info.TypeId : 0;
        }

        /// <summary>Plays the slot's cue of an entity type; a type id of 0 is ignored.</summary>
        public void Add(int typeId, SoundSlot slot, float3 position, byte faction)
        {
            if (typeId != 0)
            {
                _events[_queue].Add(new SoundEvent { TypeId = typeId, Slot = slot, Faction = faction, Position = position });
            }
        }
    }
}
