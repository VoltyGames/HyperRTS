using HyperRTS.Core;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Production;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace HyperRTS.Simulation.Animation
{
    /// <summary>A door under a Producer that opens for each unit it releases (a roll-up or hangar door).</summary>
    [AddComponentMenu(HyperRTSMenu.Animation + "Production Door")]
    [Icon(HyperRTSIcons.Animation)]
    [HelpURL(HyperRTSDocs.Animation)]
    [DisallowMultipleComponent]
    public class ProductionDoorAuthoring : AuthoringBehaviour
    {
        [Tooltip("Move from shut to open in the parent's axes, in metres (a roll-up door rises its own height).")]
        public Vector3 openOffset;

        [Tooltip("Local axis the door turns about to open.")]
        public Vector3 openAxis = Vector3.right;

        [Tooltip("Degrees it turns about the axis to open; 0 for a door that only moves.")]
        public float openAngle;

        [Tooltip("Seconds to open, and again to close.")]
        [Min(0.1f)]
        public float moveSeconds = 1.5f;

        [Tooltip("Seconds it stays open while the unit drives out.")]
        [Min(0f)]
        public float holdSeconds = 3f;

        public class Baker : Baker<ProductionDoorAuthoring>
        {
            public override void Bake(ProductionDoorAuthoring authoring)
            {
                var producer = GetComponentInParent<ProducerAuthoring>();
                if (producer == null)
                {
                    return;
                }

                var rest = GetComponent<Transform>();
                var writer = new BakerWriter(this, GetEntity(TransformUsageFlags.Dynamic));
                writer.Add(new ProductionDoor
                {
                    Building = GetEntity(producer, TransformUsageFlags.Dynamic),
                    OpenOffset = authoring.openOffset,
                    OpenRotation = quaternion.AxisAngle(authoring.openAxis.normalized,
                        math.radians(authoring.openAngle)),
                    RestPosition = rest.localPosition,
                    RestRotation = rest.localRotation,
                    MoveSeconds = math.max(authoring.moveSeconds, 0.1f),
                    HoldSeconds = authoring.holdSeconds,
                    Elapsed = float.MaxValue,
                });
            }
        }
    }
}
