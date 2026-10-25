using HyperRTS.Client.Common;
using HyperRTS.Client.Interaction;
using HyperRTS.Simulation.Buildings;
using HyperRTS.Simulation.Resources;
using Unity.Entities;
using UnityEngine;

namespace HyperRTS.Presentation.HUD
{
    /// <summary>
    /// A dragged line's total cost above and right of its end, in the short colour while the player can't pay it.
    /// </summary>
    public sealed class LineCost : HUDPanel
    {
        private static readonly Vector2 Offset = new(16f, -8f);

        private readonly LiveQuery _placement = new(entityManager =>
            entityManager.CreateEntityQuery(ComponentType.ReadOnly<PlacementSegment>()));

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
            var segments = IsDraggingLine(context.EntityManager, placement) ? CountValid(context.EntityManager) : 0;
            Root.SetHidden(segments == 0);
            if (segments == 0)
            {
                return;
            }

            var costs = context.EntityManager.GetBuffer<ResourceCost>(placement.Prefab, true);
            if (placement.Prefab != _prefab || segments != _segments)
            {
                _prefab = placement.Prefab;
                _segments = segments;
                _costs.Bind(costs, _segments);
            }

            _costs.MarkShort(!ResourceMath.CanAfford(context.Stock.AsNativeArray(), costs, _segments));
            // The drag's end sits under the cursor, so the row follows it without reading device input.
            PlaceBeside(placement.Position, Offset);
        }

        // A single building drags too, to turn, and leaves the last line's segments in the buffer.
        private static bool IsDraggingLine(EntityManager entityManager, in PlacementState placement)
        {
            if (!placement.Active || !placement.Dragging)
            {
                return false;
            }

            return entityManager.HasComponent<LinePlacement>(placement.Prefab);
        }

        private int CountValid(EntityManager entityManager)
        {
            var placement = _placement.In(entityManager).GetSingletonEntity();
            return PlacementSegment.CountValid(entityManager.GetBuffer<PlacementSegment>(placement, true));
        }
    }
}
