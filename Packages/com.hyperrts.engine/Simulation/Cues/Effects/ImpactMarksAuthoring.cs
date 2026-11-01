using HyperRTS.Simulation.Combat.Weapons;
using HyperRTS.Simulation.Common.Authoring;
using System.Collections.Generic;
using HyperRTS.Core;
using Unity.Entities;
using UnityEngine;

namespace HyperRTS.Simulation.Cues.Effects
{
    /// <summary>Craters and scorch marks this unit's or building's shots leave where they land on open ground.</summary>
    [AddComponentMenu(HyperRTSMenu.Cues + "Impact Marks")]
    [Icon(HyperRTSIcons.Combat)]
    [HelpURL(HyperRTSDocs.Combat)]
    [DisallowMultipleComponent]
    [RequiresAuthoring(typeof(WeaponAuthoring), "a Weapon")]
    public class ImpactMarksAuthoring : AuthoringBehaviour
    {
        [Tooltip("Marks every hit leaves on open ground (never on buildings, bridges, water or aircraft), e.g. a crater " +
                 "and a scorch.")]
        public ImpactMark[] marks = { };

        /// <summary>Unset entries are skipped; an entity left without marks writes no impact events.</summary>
        public static void Add(EntityWriter writer, IReadOnlyList<ImpactMark> marks)
        {
            var buffer = writer.AddBuffer<EntityImpactMark>();
            foreach (var mark in marks)
            {
                if (mark != null)
                {
                    buffer.Add(new EntityImpactMark { Mark = mark });
                }
            }
        }

        public class Baker : Baker<ImpactMarksAuthoring>
        {
            public override void Bake(ImpactMarksAuthoring authoring)
            {
                var writer = this.Writer();
                ImpactMarksAuthoring.Add(writer, authoring.marks);
            }
        }
    }
}
