using HyperRTS.Core;
using Unity.Entities;
using UnityEngine;

namespace HyperRTS.Simulation.Units
{
    /// <summary>Bakes the <see cref="UnitTag"/> marker, matching <c>UnitEntityFactory</c> output.</summary>
    [AddComponentMenu(HyperRTSMenu.Units + "Unit Tag")]
    [Icon(HyperRTSIcons.Units)]
    [HelpURL(HyperRTSDocs.WorldSetup)]
    [DisallowMultipleComponent]
    public class UnitTagAuthoring : MonoBehaviour
    {
        public class Baker : Baker<UnitTagAuthoring>
        {
            public override void Bake(UnitTagAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent<UnitTag>(entity);
            }
        }
    }
}
