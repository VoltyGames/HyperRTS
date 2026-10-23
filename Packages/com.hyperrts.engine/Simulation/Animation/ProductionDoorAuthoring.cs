using HyperRTS.Core;
using HyperRTS.Simulation.Air;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Production;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace HyperRTS.Simulation.Animation
{
    /// <summary>
    /// A door under a Producer that opens for each unit it releases (a roll-up or hangar door), or under an aircraft
    /// that stays open through its drop run (a cargo ramp or bomb bay).
    /// </summary>
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

        [Tooltip("Own-axis scale when open, as a factor of the shut scale. A roll-up curtain pivoted at its top opens at "
            + "(1, 0.05, 1) and coils up toward the pivot.")]
        public Vector3 openScale = Vector3.one;

        [Tooltip("Seconds to open, and again to close.")]
        [Min(0.1f)]
        public float moveSeconds = 1.5f;

        [Tooltip("Seconds it stays open while the unit drives out.")]
        [Min(0f)]
        public float holdSeconds = 3f;

        private bool Scales => openScale != Vector3.one;

        public class Baker : Baker<ProductionDoorAuthoring>
        {
            public override void Bake(ProductionDoorAuthoring authoring)
            {
                var root = Root();
                if (root == null)
                {
                    return;
                }

                var rest = GetComponent<Transform>();
                // A non-uniform scale needs a PostTransformMatrix, which LocalTransform alone can't carry.
                var flags = authoring.Scales
                    ? TransformUsageFlags.Dynamic | TransformUsageFlags.NonUniformScale
                    : TransformUsageFlags.Dynamic;
                var writer = new BakerWriter(this, GetEntity(flags));
                writer.Add(new ProductionDoor
                {
                    Root = GetEntity(root, TransformUsageFlags.Dynamic),
                    OpenOffset = authoring.openOffset,
                    OpenRotation = quaternion.AxisAngle(authoring.openAxis.normalized,
                        math.radians(authoring.openAngle)),
                    OpenScale = authoring.openScale,
                    RestPosition = rest.localPosition,
                    RestRotation = rest.localRotation,
                    RestScale = rest.localScale,
                    MoveSeconds = math.max(authoring.moveSeconds, 0.1f),
                    HoldSeconds = authoring.holdSeconds,
                    Elapsed = float.MaxValue,
                });
            }

            private Component Root()
            {
                var producer = GetComponentInParent<ProducerAuthoring>();
                return producer != null ? producer : GetComponentInParent<FlightAuthoring>();
            }
        }
    }
}
