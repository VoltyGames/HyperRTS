using UnityEngine.UIElements;

namespace HyperRTS.Presentation.HUD
{
    /// <summary>Shorthands for the HUD's plain elements and visibility; styling lives in HUD.uss.</summary>
    public static class HUDElements
    {
        public static VisualElement Box(string className, VisualElement parent = null)
        {
            var element = new VisualElement();
            element.AddToClassList(className);
            parent?.Add(element);
            return element;
        }

        public static Label Text(string text, string className, VisualElement parent = null)
        {
            var label = new Label(text);
            label.AddToClassList(className);
            parent?.Add(label);
            return label;
        }

        /// <summary>Removes the element from layout.</summary>
        public static void SetVisible(this VisualElement element, bool visible) =>
            element.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;

        /// <summary>Hides the element but keeps its space, so neighbouring panels don't shift.</summary>
        public static void SetShown(this VisualElement element, bool shown) =>
            element.style.visibility = shown ? Visibility.Visible : Visibility.Hidden;
    }
}
