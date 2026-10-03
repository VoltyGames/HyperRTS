using HyperRTS.Core;
using Unity.Entities;
using UnityEngine;

namespace HyperRTS.Simulation.Buildings
{
    [AddComponentMenu(HyperRTSMenu.Buildings + "Construction Progress")]
    [Icon(HyperRTSIcons.Buildings)]
    [HelpURL(HyperRTSDocs.WorldSetup)]
    [DisallowMultipleComponent]
    public class ConstructionProgressAuthoring : MonoBehaviour
    {
        [Tooltip("Initial construction progress: 0 = just started, 1 = complete.")]
        [Range(0f, 1f)]
        public float progress;

        public class Baker : Baker<ConstructionProgressAuthoring>
        {
            public override void Bake(ConstructionProgressAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity, new ConstructionProgress { Value = authoring.progress });
                SetComponentEnabled<ConstructionProgress>(entity, authoring.progress < 1f);
            }
        }
    }

    /// <summary>
    /// Build progress, 0..1. Enableable: enabled while under construction, disabled by
    /// <see cref="ConstructionSystem"/> once complete (query <c>WithDisabled</c> for finished buildings).
    /// </summary>
    public struct ConstructionProgress : IComponentData, IEnableableComponent
    {
        public float Value;
    }
}
