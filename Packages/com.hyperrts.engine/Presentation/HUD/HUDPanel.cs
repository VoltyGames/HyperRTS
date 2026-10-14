using UnityEngine.UIElements;

namespace HyperRTS.Presentation.HUD
{
    /// <summary>One HUD widget: its element tree, refreshed every frame from the <see cref="HUDContext"/>.</summary>
    public abstract class HUDPanel
    {
        /// <summary>A boxed panel gets <c>hud-panel</c> as well as <paramref name="className"/>.</summary>
        protected HUDPanel(string className, bool boxed = true)
        {
            Root = HUDElements.Box(className);
            if (boxed)
            {
                Root.AddToClassList("hud-panel");
            }
        }

        public VisualElement Root { get; }

        /// <summary>Whether world clicks under the panel are ignored (false for pass-through overlays).</summary>
        public virtual bool BlocksPointer => true;

        /// <summary>Shown only with a local player: observers have no stockpile, selection or commands.</summary>
        public virtual bool NeedsPlayer => true;

        public abstract void Refresh(HUDContext context);
    }
}
