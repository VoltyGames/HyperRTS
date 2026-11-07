using HyperRTS.Presentation.Common;
using HyperRTS.Presentation.Interaction;
using HyperRTS.Simulation.Buildings.Placement;
using UnityEngine;
using UnityEngine.UIElements;

namespace HyperRTS.Presentation.HUD.Panels
{
    /// <summary>How to turn a single building, below and right of its placement ghost.</summary>
    public sealed class PlacementHint : HUDPanel
    {
        private readonly LiveSingleton<PlacementKeys> _keys = new();
        private readonly Label _hint;
        private PlacementKeys _shownKeys;

        public PlacementHint(Label hint)
            : base(hint)
        {
            _hint = hint;
            Root.SetHidden(true);
        }

        public override bool BlocksPointer => false;

        public override void Refresh(HUDContext context)
        {
            var placement = context.Placement;
            var keys = _keys.Get(context.EntityManager);
            var shown = placement.Active && keys.Rotate.Length > 0
                        && !context.EntityManager.HasComponent<LinePlacement>(placement.Prefab);
            Root.SetHidden(!shown);
            if (!shown)
            {
                return;
            }

            if (keys.Rotate != _shownKeys.Rotate || keys.Snap != _shownKeys.Snap)
            {
                _shownKeys = keys;
                _hint.text = string.Format(HUDText.Text("hud.rotate-hint"), keys.Rotate, keys.Snap);
            }

            PlaceBeside(placement.Position, Vector2.zero);
        }
    }
}
