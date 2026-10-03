using HyperRTS.Core;
using Unity.Entities;
using UnityEngine;

namespace HyperRTS.Simulation.Resources
{
    /// <summary>Harvesters of the same owner deposit cargo here (supply center, town hall).</summary>
    [AddComponentMenu(HyperRTSMenu.Resources + "Resource Drop-Off")]
    [Icon(HyperRTSIcons.Resources)]
    [HelpURL(HyperRTSDocs.Modules)]
    [DisallowMultipleComponent]
    public class ResourceDropOffAuthoring : MonoBehaviour
    {
        public class Baker : Baker<ResourceDropOffAuthoring>
        {
            public override void Bake(ResourceDropOffAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent<ResourceDropOff>(entity);
            }
        }
    }

    public struct ResourceDropOff : IComponentData { }
}
