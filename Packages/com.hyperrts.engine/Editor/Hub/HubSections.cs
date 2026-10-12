using UnityEngine.UIElements;

namespace HyperRTS.Editor.Hub
{
    /// <summary>Shared layout for Tools pages and inspectors: a titled section and rows of equal buttons.</summary>
    internal static class HubSections
    {
        public static VisualElement Section(string title)
        {
            var section = new VisualElement();
            section.AddToClassList("hrts-section");
            var label = new Label(title);
            label.AddToClassList("hrts-section__title");
            section.Add(label);
            return section;
        }

        public static VisualElement Buttons(params VisualElement[] buttons)
        {
            var row = new VisualElement();
            row.AddToClassList("hrts-buttons");
            foreach (var button in buttons)
            {
                row.Add(button);
            }

            return row;
        }
    }
}
