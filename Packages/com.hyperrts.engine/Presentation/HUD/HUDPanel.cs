using UnityEngine.UIElements;

namespace HyperRTS.Presentation.HUD
{
    /// <summary>A boxed HUD panel (<c>hud-panel</c> plus its own USS class) that blocks world clicks under it.</summary>
    public abstract class HUDPanel : IHUDPanel
    {
        protected HUDPanel(string className)
        {
            Root = HUDElements.Box(className);
            Root.AddToClassList("hud-panel");
        }

        public VisualElement Root { get; }

        public virtual bool BlocksPointer => true;

        public abstract void Refresh(HUDContext context);
    }
}
