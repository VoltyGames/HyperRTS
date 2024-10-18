using Unity.Entities;
using UnityEngine;

namespace HyperRTS.Core.Buildings
{
    public class ConstructionProgressAuthoring : MonoBehaviour
    {
        public float progress;

        public class Baker : Baker<ConstructionProgressAuthoring>
        {
            public override void Bake(ConstructionProgressAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity, new ConstructionProgress { Value = authoring.progress });
            }
        }
    }
    
    public struct ConstructionProgress : IComponentData
    {
        public float Value;
    }
}
