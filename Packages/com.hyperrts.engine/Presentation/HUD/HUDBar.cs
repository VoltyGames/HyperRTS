using UnityEngine;
using UnityEngine.UIElements;

namespace HyperRTS.Presentation.HUD
{
    /// <summary>A track with a fill whose width shows a 0 to 1 fraction.</summary>
    [UxmlElement]
    public partial class HUDBar : VisualElement
    {
        private readonly VisualElement _fill = new();

        public HUDBar()
        {
            AddToClassList("hud-bar");
            _fill.AddToClassList("hud-bar__fill");
            Add(_fill);
        }

        public float Fraction
        {
            set => _fill.style.width = Length.Percent(Mathf.Clamp01(value) * 100f);
        }
    }
}
