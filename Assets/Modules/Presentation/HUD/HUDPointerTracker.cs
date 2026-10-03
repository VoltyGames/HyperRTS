using System.Collections.Generic;
using UnityEngine.UIElements;

namespace HyperRTS.Presentation.HUD
{
    /// <summary>Knows whether the pointer is over a HUD panel, without reading device input directly.</summary>
    public sealed class HUDPointerTracker
    {
        private readonly HashSet<VisualElement> _hovered = new();

        public void Track(VisualElement panel)
        {
            panel.RegisterCallback<PointerEnterEvent>(_ => _hovered.Add(panel));
            panel.RegisterCallback<PointerLeaveEvent>(_ => _hovered.Remove(panel));
        }

        public void Clear() => _hovered.Clear();

        /// <summary>Ignores panels hidden while hovered, which never receive their leave event.</summary>
        public bool IsOverUI()
        {
            foreach (var panel in _hovered)
            {
                if (panel.panel != null && panel.resolvedStyle.display != DisplayStyle.None && panel.visible)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
