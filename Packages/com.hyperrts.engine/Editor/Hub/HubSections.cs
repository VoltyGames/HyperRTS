using UnityEngine.UIElements;

namespace HyperRTS.Editor.PlayMode
{
    /// <summary>Shared layout for the Play page's sections: a titled block and rows of equal buttons.</summary>
    internal static class PlaySections
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

        public static VisualElement Buttons(params Button[] buttons)
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
