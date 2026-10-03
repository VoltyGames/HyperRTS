using HyperRTS.Core;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace HyperRTS.Simulation.Units
{
    [AddComponentMenu(HyperRTSMenu.Units + "Move Destination")]
    [Icon(HyperRTSIcons.Units)]
    [HelpURL(HyperRTSDocs.WorldSetup)]
    [DisallowMultipleComponent]
    public class MoveDestinationAuthoring : MonoBehaviour
    {
        [Tooltip("World-space position to move toward.")]
        public Vector3 destination;

        public class Baker : Baker<MoveDestinationAuthoring>
        {
            public override void Bake(MoveDestinationAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity, new MoveDestination { Value = authoring.destination });
            }
        }
    }

    /// <summary>
    /// World-space move order. Enableable: set the value and enable it to issue an order;
    /// <see cref="MovementSystem"/> disables it on arrival, so orders never change the archetype.
    /// </summary>
    public struct MoveDestination : IComponentData, IEnableableComponent
    {
        public float3 Value;
    }
}
