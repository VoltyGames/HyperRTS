using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Cues.Announcements;
using HyperRTS.Simulation.Cues.Effects;
using HyperRTS.Simulation.Cues.Sounds;
using HyperRTS.Simulation.Cues.Surfaces;
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
        [ReadOnly] private ComponentLookup<DeathRemains> _remains;
        [ReadOnly] private ComponentLookup<EntityInfo> _info;
        private CoverLookup _cover;
        private Entity _queue;

        public CueWriter(ref SystemState state) : this()
        {
            _events = state.GetBufferLookup<CueEvent>();
            _sounds = state.GetBufferLookup<EntitySound>(true);
            _effects = state.GetBufferLookup<EntityEffect>(true);
            _marks = state.GetBufferLookup<EntityImpactMark>(true);
            _impactEffects = state.GetComponentLookup<EntityImpactEffects>(true);
            _remains = state.GetComponentLookup<DeathRemains>(true);
            _info = state.GetComponentLookup<EntityInfo>(true);
            _cover = new CoverLookup(ref state);
        }

        /// <summary>Call each update before scheduling; the queue exists once <see cref="CueClearSystem"/> is created.</summary>
        public void Update(ref SystemState state, Entity queue)
        {
            _events.Update(ref state);
            _sounds.Update(ref state);
            _effects.Update(ref state);
            _marks.Update(ref state);
            _impactEffects.Update(ref state);
            _remains.Update(ref state);
            _info.Update(ref state);
            _cover.Update(ref state);
            _queue = queue;
        }

        /// <summary>
        /// Plays <paramref name="source"/>'s cue for the slot and variant (prefab or instance), at its effect socket
        /// placed by <paramref name="transform"/> when it has a rig; false if it has neither sound nor effect for it.
        /// </summary>
        public bool Play(Entity source, CueSlot slot, in LocalTransform transform, float3 direction, byte faction,
            byte variant = 0)
        {
            var position = transform.Position;
            var rigged = _effects.TryGetBuffer(source, out var effects);
            if (rigged && TryGetSocket(effects, slot, variant, out var socket))
            {
                position = transform.TransformPoint(socket);
            }

            return PlayAt(source, slot, position, direction, faction, variant);
        }

        /// <summary>Plays <paramref name="source"/>'s cue at <paramref name="position"/>, such as a muzzle turned with its turret.</summary>
        public bool PlayAt(Entity source, CueSlot slot, float3 position, float3 direction, byte faction,
            byte variant = 0)
        {
            var typeId = TypeIdFor(source, slot, variant);
            // A concealed source's cues stay as hidden as a stealthed one's.
            var hidden = _cover.Find(source) != Cover.None;
            return Add(typeId, slot, position, direction, faction, hidden, variant: variant);
        }

        /// <summary>
        /// <paramref name="source"/>'s type id if it has a sound or an effect for the slot and variant (for impacts
        /// also marks or impact effects, for deaths also remains), else 0.
        /// </summary>
        public int TypeIdFor(Entity source, CueSlot slot, byte variant = 0)
        {
            if (!HasCue(source, slot, variant))
            {
                return 0;
            }

            return _info.TryGetComponent(source, out var info) ? info.TypeId : 0;
        }

        /// <summary>Plays the slot's cue of an entity type; a type id of 0 is ignored and returns false.</summary>
        public bool Add(int typeId, CueSlot slot, float3 position, float3 direction, byte faction,
            bool stealthed = false, float radius = 0f, Entity target = default, byte variant = 0)
        {
            if (typeId == 0)
            {
                return false;
            }

            _events[_queue].Add(new CueEvent
            {
                TypeId = typeId, Slot = slot, Variant = variant, Faction = faction, Position = position,
                Direction = direction, Stealthed = stealthed, Radius = radius, Target = target,
            });
            return true;
        }

        /// <summary>
        /// Queues an announcer alert for <paramref name="faction"/>'s player. It needs no sound on the source, since
        /// the listener's announcer picks the line; <paramref name="typeId"/> names the subject, 0 for none.
        /// </summary>
        public void Announce(AnnouncementKind kind, byte faction, float3 position, int typeId) =>
            _events[_queue].Add(new CueEvent
            {
                TypeId = typeId, Slot = CueSlot.Announce, Variant = kind.Value, Faction = faction, Position = position,
                Direction = math.forward(),
            });

        private bool HasCue(Entity source, CueSlot slot, byte variant)
        {
            if (slot == CueSlot.Impact && LeavesImpact(source))
            {
                return true;
            }

            if (slot == CueSlot.Death && _remains.HasComponent(source))
            {
                return true;
            }

            if (_sounds.TryGetBuffer(source, out var sounds) && PlaysSound(sounds, slot, variant))
            {
                return true;
            }

            var rigged = _effects.TryGetBuffer(source, out var effects);
            return rigged && TryGetSocket(effects, slot, variant, out _);
        }

        private bool LeavesImpact(Entity shooter)
        {
            var marked = _marks.TryGetBuffer(shooter, out var marks) && !marks.IsEmpty;
            return marked || _impactEffects.HasComponent(shooter);
        }

        private static bool PlaysSound(DynamicBuffer<EntitySound> sounds, CueSlot slot, byte variant)
        {
            foreach (var sound in sounds)
            {
                if (sound.Plays(slot, variant))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool TryGetSocket(DynamicBuffer<EntityEffect> effects, CueSlot slot, byte variant,
            out float3 socket)
        {
            foreach (var effect in effects)
            {
                if (effect.Plays(slot, variant))
                {
                    socket = effect.Socket;
                    return true;
                }
            }

            socket = float3.zero;
            return false;
        }
    }
}
