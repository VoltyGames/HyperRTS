using HyperRTS.Simulation.Buildings;
using HyperRTS.Simulation.Common.Authoring;
using Unity.Entities;
using UnityEngine;

namespace HyperRTS.Simulation.Resources.Gathering
{
    /// <summary>Harvesters of the same owner deposit cargo here (supply center, town hall).</summary>
    [AddComponentMenu(HyperRTSMenu.Resources + "Resource Drop-Off")]
    [Icon(HyperRTSIcons.Resources)]
    [HelpURL(HyperRTSDocs.Resources)]
    [DisallowMultipleComponent]
    [RequiresAuthoring(typeof(BuildingAuthoring), "a Building")]
    public class ResourceDropOffAuthoring : AuthoringBehaviour
    {
        public class Baker : Baker<ResourceDropOffAuthoring>
        {
            public override void Bake(ResourceDropOffAuthoring authoring)
            {
                var writer = this.Writer();
                writer.Add<ResourceDropOff>();
            }
        }
    }
}
