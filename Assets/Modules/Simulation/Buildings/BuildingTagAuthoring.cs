using HyperRTS.Core;
using Unity.Entities;
using UnityEngine;

namespace HyperRTS.Simulation.Buildings
{
    /// <summary>Bakes the <see cref="BuildingTag"/> marker. Required: <c>ConstructionSystem</c> queries it.</summary>
    [AddComponentMenu(HyperRTSMenu.Buildings + "Building Tag")]
    [Icon(HyperRTSIcons.Buildings)]
    [HelpURL(HyperRTSDocs.WorldSetup)]
    [DisallowMultipleComponent]
    public class BuildingTagAuthoring : MonoBehaviour
    {
        public class Baker : Baker<BuildingTagAuthoring>
        {
            public override void Bake(BuildingTagAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent<BuildingTag>(entity);
            }
        }
    }
}
