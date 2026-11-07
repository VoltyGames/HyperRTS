using HyperRTS.Simulation.Common.Authoring;
using HyperRTS.Simulation.Cues.Voice;
using System.Collections.Generic;
using HyperRTS.Simulation.GameEntities;
using Unity.Entities;
using UnityEngine;

namespace HyperRTS.Simulation.Cues.Sounds
{
    /// <summary>
    /// Sound cues a unit or building plays when it fires, dies, is produced, finishes production or answers its owner.
    /// </summary>
    [AddComponentMenu(HyperRTSMenu.Look + "Audio/Entity Sounds")]
    [Icon(HyperRTSIcons.EntitySoundsAuthoring)]
    [HelpURL(HyperRTSDocs.Audio)]
    [DisallowMultipleComponent]
    [RequiresAuthoring(typeof(GameEntityAuthoring), "a Unit or Building")]
    public class EntitySoundsAuthoring : AuthoringBehaviour
    {
        [Header("World")]
        [Tooltip("Each shot, at the shooter.")]
        public SoundCue fire;

        [Tooltip("Where each shot lands.")]
        public SoundCue impact;

        [Tooltip("On death, where it dies.")]
        public SoundCue death;

        [Tooltip("When it uses an ability.")]
        public SoundCue ability;

        [Tooltip("Each shot of the extra weapons, in order; one without an entry fires silently.")]
        public List<SoundCue> mountFire = new();

        [Header("Owner only")]
        [Tooltip("At this producer when a unit or research in its queue finishes.")]
        public SoundCue productionDone;

        [Tooltip("Voice: when it uses an ability.")]
        public SoundCue abilityVoice;

        [Tooltip("Voice: when it takes damage after a quiet spell, throttled per unit and per player.")]
        public SoundCue underFire;

        [Tooltip("Squad voice: a halted squad's members reach cover.")]
        public SoundCue takeCover;

        [Tooltip("Squad voice: its suppression rises to suppressed.")]
        public SoundCue suppressed;

        [Tooltip("Squad voice: its suppression rises to pinned.")]
        public SoundCue pinned;

        [Tooltip("Squad voice: it starts to retreat.")]
        public SoundCue retreat;

        [Tooltip("Squad voice: its members start digging.")]
        public SoundCue digIn;

        [Tooltip("Squad voice: a dead member comes back.")]
        public SoundCue reinforced;

        [Tooltip("When it is produced or its construction finishes.")]
        public SoundCue ready;

        [Tooltip("When the owner selects it.")]
        public SoundCue select;

        [Tooltip("When the owner orders it to move.")]
        public SoundCue move;

        [Tooltip("When the owner orders it to attack.")]
        public SoundCue attack;

        /// <summary>Call before <see cref="Add"/>: the under fire voice remembers the last hit.</summary>
        public static void AddUnderFire(EntityWriter writer) =>
            writer.Add<UnderFireVoice>();

        public static DynamicBuffer<EntitySound> Add(EntityWriter writer) =>
            writer.AddBuffer<EntitySound>();

        /// <summary>Unset cues are skipped, so the slot stays silent and writes no events.</summary>
        public static void Set(DynamicBuffer<EntitySound> sounds, CueSlot slot, SoundCue cue, byte variant = 0)
        {
            if (cue != null)
            {
                sounds.Add(new EntitySound { Slot = slot, Variant = variant, Cue = cue });
            }
        }

        public class Baker : Baker<EntitySoundsAuthoring>
        {
            public override void Bake(EntitySoundsAuthoring authoring)
            {
                var writer = this.Writer();
                if (authoring.underFire != null)
                {
                    EntitySoundsAuthoring.AddUnderFire(writer);
                }

                var sounds = EntitySoundsAuthoring.Add(writer);
                EntitySoundsAuthoring.Set(sounds, CueSlot.Fire, authoring.fire);
                EntitySoundsAuthoring.Set(sounds, CueSlot.Impact, authoring.impact);
                EntitySoundsAuthoring.Set(sounds, CueSlot.Death, authoring.death);
                EntitySoundsAuthoring.Set(sounds, CueSlot.Ability, authoring.ability);
                EntitySoundsAuthoring.Set(sounds, CueSlot.Ready, authoring.ready);
                EntitySoundsAuthoring.Set(sounds, CueSlot.Select, authoring.select);
                EntitySoundsAuthoring.Set(sounds, CueSlot.Move, authoring.move);
                EntitySoundsAuthoring.Set(sounds, CueSlot.Attack, authoring.attack);
                EntitySoundsAuthoring.Set(sounds, CueSlot.ProductionDone, authoring.productionDone);
                EntitySoundsAuthoring.Set(sounds, CueSlot.AbilityVoice, authoring.abilityVoice);
                EntitySoundsAuthoring.Set(sounds, CueSlot.UnderFire, authoring.underFire);
                EntitySoundsAuthoring.Set(sounds, CueSlot.TakeCover, authoring.takeCover);
                EntitySoundsAuthoring.Set(sounds, CueSlot.Suppressed, authoring.suppressed);
                EntitySoundsAuthoring.Set(sounds, CueSlot.Pinned, authoring.pinned);
                EntitySoundsAuthoring.Set(sounds, CueSlot.Retreat, authoring.retreat);
                EntitySoundsAuthoring.Set(sounds, CueSlot.DigIn, authoring.digIn);
                EntitySoundsAuthoring.Set(sounds, CueSlot.Reinforced, authoring.reinforced);
                for (var i = 0; i < authoring.mountFire.Count; i++)
                {
                    EntitySoundsAuthoring.Set(sounds, CueSlot.Fire, authoring.mountFire[i], (byte)(i + 1));
                }
            }
        }
    }
}
