using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Vision;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.Cues
{
    /// <summary>
    /// Appends to the <see cref="CueQueue"/> from a job without a sync point. Entities with neither a sound nor an
    /// effect for the slot write nothing, so plain units cost no events or bandwidth. Use from one thread.
    /// </summary>
    public struct CueWriter
    {
        private BufferLookup<CueEvent> _events;
        [ReadOnly] private BufferLookup<EntitySound> _sounds;
        [ReadOnly] private BufferLookup<EntityEffect> _effects;
        [ReadOnly] private BufferLookup<EntityImpactMark> _marks;
        [ReadOnly] private ComponentLookup<EntityImpactEffects> _impactEffects;
        [ReadOnly] private ComponentLookup<EntityInfo> _info;
        [ReadOnly] private ComponentLookup<Stealthed> _cloaks;
        private Entity _queue;

        public CueWriter(ref SystemState state) : this()
        {
            _events = state.GetBufferLookup<CueEvent>();
            _sounds = state.GetBufferLookup<EntitySound>(true);
            _effects = state.GetBufferLookup<EntityEffect>(true);
            _marks = state.GetBufferLookup<EntityImpactMark>(true);
            _impactEffects = state.GetComponentLookup<EntityImpactEffects>(true);
            _info = state.GetComponentLookup<EntityInfo>(true);
            _cloaks = state.GetComponentLookup<Stealthed>(true);
        }

        /// <summary>Call each update before scheduling; the queue exists once <see cref="CueClearSystem"/> is created.</summary>
        public void Update(ref SystemState state, Entity queue)
        {
            _events.Update(ref state);
            _sounds.Update(ref state);
            _effects.Update(ref state);
            _marks.Update(ref state);
            _impactEffects.Update(ref state);
            _info.Update(ref state);
            _cloaks.Update(ref state);
            _queue = queue;
        }

        /// <summary>
        /// Plays <paramref name="source"/>'s cue for the slot (prefab or instance), at its effect socket placed by
        /// <paramref name="transform"/> when it has a rig; false if it has neither sound nor effect for the slot.
        /// </summary>
        public bool Play(Entity source, CueSlot slot, in LocalTransform transform, float3 direction, byte faction)
        {
            var position = transform.Position;
            if (_effects.TryGetBuffer(source, out var effects) && EffectRules.TryGetSocket(effects, slot, out var socket))
            {
                position = transform.TransformPoint(socket);
            }

            return Add(TypeIdFor(source, slot), slot, position, direction, faction, _cloaks.HasEnabled(source));
        }

        /// <summary>
        /// <paramref name="source"/>'s type id if it has a sound or an effect for the slot (for impacts also marks or
        /// impact effects), else 0.
        /// </summary>
        public int TypeIdFor(Entity source, CueSlot slot)
        {
            if (!HasCue(source, slot))
            {
                return 0;
            }

            return _info.TryGetComponent(source, out var info) ? info.TypeId : 0;
        }

        /// <summary>Plays the slot's cue of an entity type; a type id of 0 is ignored and returns false.</summary>
        public bool Add(int typeId, CueSlot slot, float3 position, float3 direction, byte faction,
            bool stealthed = false, float radius = 0f, Entity target = default)
        {
            if (typeId == 0)
            {
                return false;
            }

            _events[_queue].Add(new CueEvent
            {
                TypeId = typeId, Slot = slot, Faction = faction, Position = position, Direction = direction,
                Stealthed = stealthed, Radius = radius, Target = target,
            });
            return true;
        }

        private bool HasCue(Entity source, CueSlot slot)
        {
            if (slot == CueSlot.Impact && LeavesImpact(source))
            {
                return true;
            }

            if (_sounds.TryGetBuffer(source, out var sounds) && SoundRules.HasCue(sounds, slot))
            {
                return true;
            }

            return _effects.TryGetBuffer(source, out var effects) && EffectRules.TryGetSocket(effects, slot, out _);
        }

        private bool LeavesImpact(Entity shooter)
        {
            var marked = _marks.TryGetBuffer(shooter, out var marks) && !marks.IsEmpty;
            return marked || _impactEffects.HasComponent(shooter);
        }
    }
}
