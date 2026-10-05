using UnityEngine;
using UnityEngine.UIElements;

namespace HyperRTS.Presentation.HUD
{
    /// <summary>A track with a fill whose width shows a 0..1 fraction.</summary>
    public sealed class HUDBar : VisualElement
    {
        private readonly VisualElement _fill;

        public HUDBar(string className, VisualElement parent = null)
        {
            AddToClassList(className);
            AddToClassList("hud-bar");
            _fill = HUDElements.Box("hud-bar__fill", this);
            parent?.Add(this);
        }

        public float Fraction
        {
            set => _fill.style.width = Length.Percent(Mathf.Clamp01(value) * 100f);
        }
    }
}
