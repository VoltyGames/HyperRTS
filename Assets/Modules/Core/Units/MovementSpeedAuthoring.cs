using Unity.Entities;
using UnityEngine;

namespace HyperRTS.Core.Units
{
    public class MovementSpeedAuthoring : MonoBehaviour
    {
        public float movementSpeed;

        public class Baker : Baker<MovementSpeedAuthoring>
        {
            public override void Bake(MovementSpeedAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity, new MovementSpeed { Value = authoring.movementSpeed });
            }
        }
    }
    
    /// <summary>
    /// Movement speed component for entities that have movement speed.
    /// </summary>
    public struct MovementSpeed : IComponentData
    {
        public float Value;
    }
}
