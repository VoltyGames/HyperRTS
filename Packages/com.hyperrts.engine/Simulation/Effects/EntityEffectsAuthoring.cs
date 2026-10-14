using HyperRTS.Core;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.GameEntities;
using Unity.Entities;
using UnityEngine;

namespace HyperRTS.Simulation.Effects
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

        public class Baker : Baker<EntityEffectsAuthoring>
        {
            public override void Bake(EntityEffectsAuthoring authoring)
            {
                if (authoring.muzzle != null)
                {
                    DependsOn(authoring.muzzle);
                }

                var writer = new BakerWriter(this, GetEntity(TransformUsageFlags.Dynamic));
                var effects = EffectSetup.Add(ref writer);
                var muzzle = EffectSetup.Socket(authoring.transform, authoring.muzzle);
                EffectSetup.Set(effects, CueSlot.Fire, authoring.fire, muzzle);
                EffectSetup.Set(effects, CueSlot.Death, authoring.death, default);
                EffectSetup.Set(effects, CueSlot.Ability, authoring.ability, default);
            }
        }
    }
}
