using HyperRTS.Simulation.Buildings;
using UnityEngine;
using UnityEngine.UIElements;

namespace HyperRTS.Presentation.HUD
{
    /// <summary>How to turn a single building, below and right of its placement ghost.</summary>
    public sealed class PlacementHint : HUDPanel
    {
        private static readonly Vector2 Offset = new(16f, 16f);

        public PlacementHint(Label hint)
            : base(hint)
        {
            hint.text = HUDText.Text("hud.rotate-hint");
            Root.SetHidden(true);
        }

        public override bool BlocksPointer => false;

        public override void Refresh(HUDContext context)
        {
            var placement = context.Placement;
            var shown = placement.Active && !context.EntityManager.HasComponent<LinePlacement>(placement.Prefab);
            Root.SetHidden(!shown);
            if (shown)
            {
                PlaceBeside(placement.Position, Offset);
            }
        }
    }
}
