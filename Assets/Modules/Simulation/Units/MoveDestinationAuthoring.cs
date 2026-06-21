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
    /// World-space position a unit is moving toward. Add this component to issue a
    /// move order; <see cref="MovementSystem"/> removes it once the unit arrives.
    /// </summary>
    public struct MoveDestination : IComponentData
    {
        public float3 Value;
    }
}
