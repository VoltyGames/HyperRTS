using HyperRTS.Core;
using Unity.Entities;
using UnityEngine;

namespace HyperRTS.Simulation.Units
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

                // Movers carry an idle (disabled) order slot unless authored with a starting destination.
                if (GetComponent<MoveDestinationAuthoring>() == null)
                {
                    AddComponent<MoveDestination>(entity);
                    SetComponentEnabled<MoveDestination>(entity, false);
                }
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
