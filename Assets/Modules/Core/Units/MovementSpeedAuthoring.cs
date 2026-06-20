using Unity.Entities;
using UnityEngine;

namespace HyperRTS.Core.Units
{
    [AddComponentMenu(HyperRTSMenu.Units + "Movement Speed")]
    [Icon(HyperRTSIcons.Units)]
    [HelpURL(HyperRTSDocs.WorldSetup)]
    [DisallowMultipleComponent]
    public class MovementSpeedAuthoring : MonoBehaviour
    {
        [Tooltip("Movement speed in units per second.")]
        public float movementSpeed = 5f;

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
