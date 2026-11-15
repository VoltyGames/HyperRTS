using HyperRTS.Simulation.Combat.Weapons;
using HyperRTS.Simulation.Common.Authoring;
using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;

namespace HyperRTS.Simulation.Cues.Effects
{
    /// <summary>What this unit's or building's shots leave where they land: effects by the material struck, and marks on open ground.</summary>
    [AddComponentMenu(HyperRTSMenu.Look + "Effects/Impact")]
    [Icon(HyperRTSIcons.ImpactAuthoring)]
    [HelpURL(HyperRTSDocs.Effects)]
    [DisallowMultipleComponent]
    [RequiresAuthoring(typeof(WeaponAuthoring), "a Weapon")]
    public class ImpactAuthoring : AuthoringBehaviour
    {
        [Tooltip("Rigs per surface material (dirt spray, water splash, sparks on metal). Leave empty for marks only.")]
        public SurfaceEffects effects;

        [Tooltip("Marks every hit leaves on open ground (never on buildings, bridges, water or aircraft), e.g. a crater " +
                 "and a scorch. Leave empty for effects only.")]
        public ImpactMark[] marks = { };

        public static void Add(EntityWriter writer, SurfaceEffects effects)
        {
            if (effects != null)
            {
                writer.Add(new EntityImpactEffects { Effects = effects });
            }
        }

        /// <summary>Unset entries are skipped; an entity left without marks writes no buffer.</summary>
        public static void Add(EntityWriter writer, IReadOnlyList<ImpactMark> marks)
        {
            var buffer = default(DynamicBuffer<EntityImpactMark>);
            foreach (var mark in marks)
            {
                if (mark == null)
                {
                    continue;
                }

                if (!buffer.IsCreated)
                {
                    buffer = writer.AddBuffer<EntityImpactMark>();
                }

                buffer.Add(new EntityImpactMark { Mark = mark });
            }
        }

        public class Baker : Baker<ImpactAuthoring>
        {
            public override void Bake(ImpactAuthoring authoring)
            {
                DependsOn(authoring.effects);
                var writer = this.Writer();
                Add(writer, authoring.effects);
                Add(writer, authoring.marks);
            }
        }
    }
}
