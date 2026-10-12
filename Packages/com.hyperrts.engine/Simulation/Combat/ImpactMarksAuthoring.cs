using HyperRTS.Core;
using HyperRTS.Simulation.Common;
using Unity.Entities;
using UnityEngine;

namespace HyperRTS.Simulation.Combat
{
    /// <summary>Craters and scorch marks this unit's or building's shots leave where they land on open ground.</summary>
    [AddComponentMenu(HyperRTSMenu.Combat + "Impact Marks")]
    [Icon(HyperRTSIcons.Combat)]
    [HelpURL(HyperRTSDocs.Combat)]
    [DisallowMultipleComponent]
    [RequiresAuthoring(typeof(WeaponAuthoring), "a Weapon")]
    public class ImpactMarksAuthoring : AuthoringBehaviour
    {
        [Tooltip("Marks every hit leaves on open ground (never on buildings, bridges, water or aircraft), e.g. a crater " +
                 "and a scorch.")]
        public ImpactMark[] marks = { };

        public class Baker : Baker<ImpactMarksAuthoring>
        {
            public override void Bake(ImpactMarksAuthoring authoring)
            {
                var writer = new BakerWriter(this, GetEntity(TransformUsageFlags.Dynamic));
                ImpactMarkSetup.Add(ref writer, authoring.marks);
            }
        }
    }
}
