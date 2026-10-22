using HyperRTS.Simulation.Resources;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UIElements;

namespace HyperRTS.Presentation.HUD
{
    /// <summary>
    /// A dragged line's total cost above and right of its end, in the short colour while the player can't pay it.
    /// </summary>
    public sealed class LineCost : HUDPanel
    {
        private static readonly Vector2 Offset = new(16f, -8f);

        private readonly CostRow _costs;
        private Entity _prefab;
        private int _segments;

        public LineCost(CostRow costs)
            : base(costs)
        {
            _costs = costs;
            Root.SetHidden(true);
        }

        public override bool BlocksPointer => false;

        public override void Refresh(HUDContext context)
        {
            var placement = context.Placement;
            var shown = placement.Active && placement.Dragging && placement.ValidSegments > 0;
            Root.SetHidden(!shown);
            if (!shown)
            {
                return;
            }

            var costs = context.EntityManager.GetBuffer<ResourceCost>(placement.Prefab, true);
            if (placement.Prefab != _prefab || placement.ValidSegments != _segments)
            {
                _prefab = placement.Prefab;
                _segments = placement.ValidSegments;
                _costs.Bind(costs, _segments);
            }

            _costs.MarkShort(!ResourceMath.CanAfford(context.Stock.AsNativeArray(), costs, _segments));
            Place(placement.Position);
        }

        // The drag's end sits under the cursor, so the row follows it without reading device input.
        private void Place(float3 point)
        {
            var camera = Camera.main;
            if (camera == null || Root.panel == null)
            {
                return;
            }

            var position = RuntimePanelUtils.CameraTransformWorldToPanel(Root.panel, point, camera);
            Root.style.left = position.x + Offset.x;
            Root.style.top = position.y + Offset.y;
        }
    }
}
