using HyperRTS.Simulation.Common.Authoring;
using System;
using System.Collections.Generic;
using HyperRTS.Simulation.GameEntities;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace HyperRTS.Simulation.Cues.Effects
{
    /// <summary>Effect rigs a unit or building plays when it fires, dies or uses an ability.</summary>
    [AddComponentMenu(HyperRTSMenu.Effects + "Entity Effects")]
    [Icon(HyperRTSIcons.Effects)]
    [HelpURL(HyperRTSDocs.Effects)]
    [DisallowMultipleComponent]
    [RequiresAuthoring(typeof(GameEntityAuthoring), "a Unit or Building")]
    public class EntityEffectsAuthoring : AuthoringBehaviour
    {
        [Tooltip("Each shot, at the muzzle, facing the target (muzzle flash, smoke).")]
        public GameObject fire;

        [Tooltip("Child transform the fire rig plays at; none plays it at the root.")]
        public Transform muzzle;

        [Tooltip("On death, where it dies (explosion, collapse dust).")]
        public GameObject death;

        [Tooltip("When it uses an ability, at the aim point.")]
        public GameObject ability;

        [Tooltip("Each shot of the extra weapons, in order; one without an entry shows nothing.")]
        public List<MountFire> mountFire = new();

        /// <summary>The fire rig of one extra weapon.</summary>
        [Serializable]
        public class MountFire
        {
            [Tooltip("Each shot, at the muzzle, facing the target.")]
            public GameObject rig;

            [Tooltip("Child transform the rig plays at; none plays it at the root.")]
            public Transform muzzle;
        }

        public static DynamicBuffer<EntityEffect> Add(EntityWriter writer) =>
            writer.AddBuffer<EntityEffect>();

        /// <summary>Unset rigs are skipped, so the entity writes no events for them.</summary>
        public static void Set(DynamicBuffer<EntityEffect> effects, CueSlot slot, GameObject rig, float3 socket,
            byte variant = 0)
        {
            if (rig != null)
            {
                effects.Add(new EntityEffect { Slot = slot, Variant = variant, Rig = rig, Socket = socket });
            }
        }

        public class Baker : Baker<EntityEffectsAuthoring>
        {
            public override void Bake(EntityEffectsAuthoring authoring)
            {
                var writer = this.Writer();
                var effects = Add(writer);
                var muzzle = Socket(this, authoring.transform, authoring.muzzle);
                Set(effects, CueSlot.Fire, authoring.fire, muzzle);
                Set(effects, CueSlot.Death, authoring.death, default);
                Set(effects, CueSlot.Ability, authoring.ability, default);
                for (var i = 0; i < authoring.mountFire.Count; i++)
                {
                    var mount = authoring.mountFire[i];
                    Set(effects, CueSlot.Fire, mount.rig,
                        Socket(this, authoring.transform, mount.muzzle), (byte)(i + 1));
                }
            }
        }
    }
}
