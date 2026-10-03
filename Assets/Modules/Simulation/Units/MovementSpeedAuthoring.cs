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

                // Idle order slot, unless a starting destination is authored.
                if (GetComponent<MoveDestinationAuthoring>() == null)
                {
                    AddComponent<MoveDestination>(entity);
                    SetComponentEnabled<MoveDestination>(entity, false);
                }
            }
        }
    }

    public struct MovementSpeed : IComponentData
    {
        public float Value;
    }
}
