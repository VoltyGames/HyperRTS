using HyperRTS.Core;
using Unity.Entities;
using UnityEngine;

namespace HyperRTS.Simulation.Buildings
{
    /// <summary>Bakes <see cref="BuildingTag"/>; required by <c>ConstructionSystem</c>.</summary>
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
