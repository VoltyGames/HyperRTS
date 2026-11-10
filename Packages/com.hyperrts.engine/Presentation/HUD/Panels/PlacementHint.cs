using HyperRTS.Presentation.Common;
using HyperRTS.Presentation.Interaction;
using HyperRTS.Simulation.Buildings.Placement;
using UnityEngine;
using UnityEngine.UIElements;

namespace HyperRTS.Presentation.HUD.Panels
{
    /// <summary>
    /// The placement's keys below and right of its ghost: how to turn a single building, or how to draw a wall.
    /// </summary>
    public sealed class PlacementHint : HUDPanel
    {
        private readonly LiveSingleton<PlacementKeys> _keys = new();
        private readonly Label _hint;
        private PlacementKeys _shownKeys;
        private bool _shownWall;

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
            var shown = placement.Active && keys.Rotate.Length > 0;
            Root.SetHidden(!shown);
            if (!shown)
            {
                return;
            }

            var wall = context.EntityManager.HasComponent<LinePlacement>(placement.Prefab);
            if (!keys.Equals(_shownKeys) || wall != _shownWall)
            {
                _shownKeys = keys;
                _shownWall = wall;
                _hint.text = wall
                    ? HUDText.Text("hud.wall-hint", keys.Place, keys.Finish, keys.Snap)
                    : HUDText.Text("hud.rotate-hint", keys.Rotate, keys.Snap);
            }

            PlaceBeside(placement.Position, Vector2.zero);
        }
    }
}
