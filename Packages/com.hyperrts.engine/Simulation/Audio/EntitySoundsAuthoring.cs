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

        public class Baker : Baker<EntitySoundsAuthoring>
        {
            public override void Bake(EntitySoundsAuthoring authoring)
            {
                var writer = new BakerWriter(this, GetEntity(TransformUsageFlags.Dynamic));
                var sounds = SoundSetup.Add(ref writer);
                SoundSetup.Set(sounds, CueSlot.Fire, authoring.fire);
                SoundSetup.Set(sounds, CueSlot.Impact, authoring.impact);
                SoundSetup.Set(sounds, CueSlot.Death, authoring.death);
                SoundSetup.Set(sounds, CueSlot.Ability, authoring.ability);
                SoundSetup.Set(sounds, CueSlot.Ready, authoring.ready);
                SoundSetup.Set(sounds, CueSlot.Select, authoring.select);
                SoundSetup.Set(sounds, CueSlot.Move, authoring.move);
                SoundSetup.Set(sounds, CueSlot.Attack, authoring.attack);
            }
        }
    }
}
