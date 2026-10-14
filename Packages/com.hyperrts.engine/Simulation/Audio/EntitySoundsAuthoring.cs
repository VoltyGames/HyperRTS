using HyperRTS.Core;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.GameEntities;
using Unity.Entities;
using UnityEngine;

namespace HyperRTS.Simulation.Audio
{
    /// <summary>Sound cues a unit or building plays when it fires, dies, is produced or answers its owner.</summary>
    [AddComponentMenu(HyperRTSMenu.Audio + "Entity Sounds")]
    [Icon(HyperRTSIcons.Audio)]
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

        [Header("Voice (owner only)")]
        [Tooltip("When it is produced or its construction finishes.")]
        public SoundCue ready;

        [Tooltip("When the owner selects it.")]
        public SoundCue select;

        [Tooltip("When the owner orders it to move.")]
        public SoundCue move;

        [Tooltip("When the owner orders it to attack.")]
        public SoundCue attack;

        public static DynamicBuffer<EntitySound> Add<TWriter>(ref TWriter writer)
            where TWriter : struct, IEntityWriter =>
            writer.AddBuffer<EntitySound>();

        /// <summary>Unset cues are skipped, so the slot stays silent and writes no events.</summary>
        public static void Set(DynamicBuffer<EntitySound> sounds, CueSlot slot, SoundCue cue)
        {
            if (cue != null)
            {
                sounds.Add(new EntitySound { Slot = slot, Cue = cue });
            }
        }

        public class Baker : Baker<EntitySoundsAuthoring>
        {
            public override void Bake(EntitySoundsAuthoring authoring)
            {
                var writer = new BakerWriter(this, GetEntity(TransformUsageFlags.Dynamic));
                var sounds = EntitySoundsAuthoring.Add(ref writer);
                EntitySoundsAuthoring.Set(sounds, CueSlot.Fire, authoring.fire);
                EntitySoundsAuthoring.Set(sounds, CueSlot.Impact, authoring.impact);
                EntitySoundsAuthoring.Set(sounds, CueSlot.Death, authoring.death);
                EntitySoundsAuthoring.Set(sounds, CueSlot.Ability, authoring.ability);
                EntitySoundsAuthoring.Set(sounds, CueSlot.Ready, authoring.ready);
                EntitySoundsAuthoring.Set(sounds, CueSlot.Select, authoring.select);
                EntitySoundsAuthoring.Set(sounds, CueSlot.Move, authoring.move);
                EntitySoundsAuthoring.Set(sounds, CueSlot.Attack, authoring.attack);
            }
        }
    }
}
