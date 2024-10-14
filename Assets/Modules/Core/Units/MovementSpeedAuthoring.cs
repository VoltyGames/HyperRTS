using Unity.Entities;
using UnityEngine;

namespace HyperRTS.Core.Units
{
    public class MovementSpeedAuthoring : MonoBehaviour
    {
        public float MovementSpeed;

        public class Baker : Baker<MovementSpeedAuthoring>
        {
            public override void Bake(MovementSpeedAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity, new MovementSpeed { Value = authoring.MovementSpeed });
            }
        }
    }
}
