using Unity.Entities;
using UnityEngine;

namespace HyperRTS.Core.Buildings
{
    public class ConstructionProgressAuthoring : MonoBehaviour
    {
        public float Progress;

        public class Baker : Baker<ConstructionProgressAuthoring>
        {
            public override void Bake(ConstructionProgressAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity, new ConstructionProgress { Value = authoring.Progress });
            }
        }
    }
}
