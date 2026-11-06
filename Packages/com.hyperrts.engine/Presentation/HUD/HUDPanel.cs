using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UIElements;

namespace HyperRTS.Presentation.HUD
{
    /// <summary>One HUD widget: its element tree, refreshed every frame from the <see cref="HUDContext"/>.</summary>
    public abstract class HUDPanel
    {
        /// <summary>Builds the panel from its UXML shell; boxed panels carry <c>hud-panel</c> there.</summary>
        protected HUDPanel(VisualTreeAsset shell)
        {
            Root = shell.CloneRoot();
        }

        /// <summary>Drives an element the HUD tree already holds.</summary>
        protected HUDPanel(VisualElement root)
        {
            Root = root;
        }

        public VisualElement Root { get; }

        /// <summary>Whether world clicks under the panel are ignored (false for pass-through overlays).</summary>
        public virtual bool BlocksPointer => true;

        /// <summary>Shown only with a shown player: observers without one have no stockpile, selection or commands.</summary>
        public virtual bool NeedsPlayer => true;

        public abstract void Refresh(HUDContext context);

        /// <summary>
        /// Pins an absolutely positioned root at left 0, top 0 beside a world point, such as the placement ghost under the
        /// cursor.
        /// </summary>
        protected void PlaceBeside(float3 point, Vector2 offset)
        {
            var camera = Camera.main;
            if (camera == null || Root.panel == null)
            {
                return;
            }

            var position = RuntimePanelUtils.CameraTransformWorldToPanel(Root.panel, point, camera);
            Root.style.translate = new Translate(position.x + offset.x, position.y + offset.y);
        }
    }
}
