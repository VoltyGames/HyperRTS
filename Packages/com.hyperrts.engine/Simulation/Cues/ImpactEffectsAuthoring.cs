using HyperRTS.Core;
using HyperRTS.Simulation.Combat;
using HyperRTS.Simulation.Common;
using Unity.Entities;
using UnityEngine;

namespace HyperRTS.Simulation.Cues
{
    /// <summary>What this unit's or building's shots throw up where they land, by the material struck.</summary>
    [AddComponentMenu(HyperRTSMenu.Effects + "Impact Effects")]
    [Icon(HyperRTSIcons.Effects)]
    [HelpURL(HyperRTSDocs.Effects)]
    [DisallowMultipleComponent]
    [RequiresAuthoring(typeof(WeaponAuthoring), "a Weapon")]
    public class ImpactEffectsAuthoring : AuthoringBehaviour
    {
        [Tooltip("Rigs per surface material (dirt spray, water splash, sparks on metal).")]
        public SurfaceEffects effects;

        public static void Add<TWriter>(ref TWriter writer, SurfaceEffects effects)
            where TWriter : struct, IEntityWriter
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
                var writer = new BakerWriter(this, GetEntity(TransformUsageFlags.Dynamic));
                Add(ref writer, authoring.effects);
            }
        }
    }
}
