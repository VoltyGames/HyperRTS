using System;
using UnityEngine.UIElements;

namespace HyperRTS.Presentation.HUD
{
    /// <summary>Shorthands for HUD templates, labels and visibility; styling lives in HUD.uss.</summary>
    public static class HUDElements
    {
        public static Label Text(string text, string className, VisualElement parent = null)
        {
            var label = new Label(text);
            label.AddToClassList(className);
            parent?.Add(label);
            return label;
        }

        /// <summary>A fresh copy of a template's one root element, without the template's wrapper.</summary>
        public static VisualElement CloneRoot(this VisualTreeAsset template) => template.Instantiate()[0];

        /// <summary>The element called <paramref name="name"/>, which the layout must have.</summary>
        public static T Require<T>(this VisualElement root, string name) where T : VisualElement =>
            root.Q<T>(name) ?? throw new InvalidOperationException(
                $"HyperRTS: the HUD layout has no {typeof(T).Name} named '{name}'.");

        /// <summary>Removes a HUD element from layout through the <c>hud-hidden</c> class.</summary>
        public static void SetHidden(this VisualElement element, bool hidden) =>
            element.EnableInClassList("hud-hidden", hidden);

        /// <summary>Hides a HUD element but keeps its space, so neighbouring panels don't shift.</summary>
        public static void SetInvisible(this VisualElement element, bool invisible) =>
            element.EnableInClassList("hud-invisible", invisible);

        /// <summary>Removes the element from layout, for elements outside the HUD's sheets.</summary>
        public static void SetVisible(this VisualElement element, bool visible) =>
            element.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
    }
}
