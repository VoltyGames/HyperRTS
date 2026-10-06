using HyperRTS.Core;
using HyperRTS.Simulation.Buildings;
using HyperRTS.Simulation.Common;
using Unity.Entities;
using UnityEngine;

namespace HyperRTS.Simulation.Resources
{
    /// <summary>Harvesters of the same owner deposit cargo here (supply center, town hall).</summary>
    [AddComponentMenu(HyperRTSMenu.Resources + "Resource Drop-Off")]
    [Icon(HyperRTSIcons.Resources)]
    [HelpURL(HyperRTSDocs.Modules)]
    [DisallowMultipleComponent]
    [RequiresAuthoring(typeof(BuildingAuthoring), "a Building")]
    public class ResourceDropOffAuthoring : AuthoringBehaviour
    {
        public class Baker : Baker<ResourceDropOffAuthoring>
        {
            public override void Bake(ResourceDropOffAuthoring authoring)
            {
                var sink = new BakerSink(this, GetEntity(TransformUsageFlags.Dynamic));
                ResourceDropOffSetup.Add(ref sink);
            }
        }
    }
}
