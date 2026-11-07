using HyperRTS.Simulation.Combat.Weapons;
using HyperRTS.Simulation.Common.Authoring;
using HyperRTS.Simulation.Cues.Surfaces;
using Unity.Entities;
using UnityEngine;

namespace HyperRTS.Simulation.Cues.Effects
{
    /// <summary>What this unit's or building's shots throw up where they land, by the material struck.</summary>
    [AddComponentMenu(HyperRTSMenu.Look + "Effects/Impact Effects")]
    [Icon(HyperRTSIcons.ImpactEffectsAuthoring)]
    [HelpURL(HyperRTSDocs.Effects)]
    [DisallowMultipleComponent]
    [RequiresAuthoring(typeof(WeaponAuthoring), "a Weapon")]
    public class ImpactEffectsAuthoring : AuthoringBehaviour
    {
        [Tooltip("Rigs per surface material (dirt spray, water splash, sparks on metal).")]
        public SurfaceEffects effects;

        public static void Add(EntityWriter writer, SurfaceEffects effects)
        {
            if (effects != null)
            {
                writer.Add(new EntityImpactEffects { Effects = effects });
            }
        }

        public class Baker : Baker<ImpactEffectsAuthoring>
        {
            public override void Bake(ImpactEffectsAuthoring authoring)
            {
                DependsOn(authoring.effects);
                var writer = this.Writer();
                Add(writer, authoring.effects);
            }
        }
    }
}
