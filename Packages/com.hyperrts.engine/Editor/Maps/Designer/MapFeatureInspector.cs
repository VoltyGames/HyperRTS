using UnityEngine.UIElements;

namespace HyperRTS.Editor.Maps.Designer
{
    /// <summary>The Map Editor's right panel: where the selected feature sits in the spec, e.g. <c>roads[3]</c>.</summary>
    public sealed class MapFeatureInspector : VisualElement
    {
        public MapFeatureInspector() => AddToClassList("hrts-map-inspector");

        public void Show(MapFeatureRef feature)
        {
            Clear();
            if (feature.IsNone)
            {
                Add(Note("Select a feature on the map or in the list."));
                return;
            }

            var title = new Label(feature.ToString());
            title.AddToClassList("hrts-section__title");
            Add(title);
            var path = new Label($"{feature.Kind.Field}[{feature.Index}]") { selection = { isSelectable = true } };
            Add(path);
        }

        private static Label Note(string text)
        {
            var note = new Label(text);
            note.AddToClassList("hrts-note");
            return note;
        }
    }
}
