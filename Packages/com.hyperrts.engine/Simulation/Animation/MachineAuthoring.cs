using HyperRTS.Core;
using HyperRTS.Simulation.Common;
using Unity.Entities;
using UnityEngine;

namespace HyperRTS.Simulation.Animation
{
    /// <summary>The root of an animated machine (pumpjack, wind turbine); its children carry Machine Parts.</summary>
    [AddComponentMenu(HyperRTSMenu.Buildings + "Machine")]
    [Icon(HyperRTSIcons.Buildings)]
    [HelpURL(HyperRTSDocs.Modules)]
    [DisallowMultipleComponent]
    public class MachineAuthoring : AuthoringBehaviour
    {
        [Tooltip("Run only while a player owns it (a captured derrick pumps, a neutral one stands still).")]
        public bool runsWhenOwned = true;

        public class Baker : Baker<MachineAuthoring>
        {
            public override void Bake(MachineAuthoring authoring)
            {
                var writer = new BakerWriter(this, GetEntity(TransformUsageFlags.Dynamic));
                writer.Add(new MachineClock { OwnedOnly = authoring.runsWhenOwned });
            }
        }
    }
}
