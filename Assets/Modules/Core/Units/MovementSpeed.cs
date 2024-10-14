using Unity.Entities;
using UnityEngine;

namespace HyperRTS.Core.Units
{
    /// <summary>
    /// Movement speed component for entities that have movement speed.
    /// </summary>
    public struct MovementSpeed : IComponentData
    {
        public float Value;
    }

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
