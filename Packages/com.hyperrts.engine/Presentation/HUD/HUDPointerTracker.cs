using System;
using System.Collections.Generic;
using Unity.Scripting.LifecycleManagement;
using UnityEngine.UIElements;

namespace HyperRTS.Presentation.HUD
{
    /// <summary>Knows whether the pointer is over a HUD panel, without reading device input directly.</summary>
    public sealed class HUDPointerTracker
    {
        [NoAutoStaticsCleanup]
        private static readonly Predicate<VisualElement> NotShown = panel => !IsShown(panel);

        private readonly HashSet<VisualElement> _hovered = new();

        public void Track(VisualElement panel)
        {
            panel.RegisterCallback<PointerEnterEvent>(_ => _hovered.Add(panel));
            panel.RegisterCallback<PointerLeaveEvent>(_ => _hovered.Remove(panel));
        }

        public void Clear() => _hovered.Clear();

        /// <summary>
        /// Forgets panels hidden or detached while hovered: they never get their leave event (a command card emptied
        /// under the pointer, say), so keeping them would claim every world click once they show again.
        /// </summary>
        public bool IsOverUI()
        {
            _hovered.RemoveWhere(NotShown);
            return _hovered.Count > 0;
        }

        private static bool IsShown(VisualElement panel)
        {
            if (panel.panel == null || !panel.visible)
            {
                return false;
            }

            for (var element = panel; element != null; element = element.hierarchy.parent)
            {
                if (element.resolvedStyle.display == DisplayStyle.None)
                {
                    return false;
                }
            }

            return true;
        }
    }
}
