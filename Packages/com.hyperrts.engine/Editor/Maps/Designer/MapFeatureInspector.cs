using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace HyperRTS.Editor.Maps.Designer
{
    /// <summary>The Map Editor's right panel: the selected feature's fields, bound to the document.</summary>
    public sealed class MapFeatureInspector : ScrollView
    {
        private readonly MapSpecDocument _document;

        public MapFeatureInspector(MapSpecDocument document)
        {
            _document = document;
            AddToClassList("hrts-map-inspector");
        }

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

            var serialized = _document.Serialized;
            serialized.Update();
            var element = serialized.FindProperty(feature.PropertyPath);
            var end = element.GetEndProperty();
            var child = element.Copy();
            for (var more = child.NextVisible(true); more && !SerializedProperty.EqualContents(child, end);
                 more = child.NextVisible(false))
            {
                var field = new PropertyField(child.Copy());
                field.Bind(serialized);
                Add(field);
            }
        }

        private static Label Note(string text)
        {
            var note = new Label(text);
            note.AddToClassList("hrts-note");
            return note;
        }
    }
}
