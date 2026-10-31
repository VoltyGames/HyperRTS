using HyperRTS.Editor.Common;
using HyperRTS.Presentation.HUD;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace HyperRTS.Editor.Maps.Designer
{
    /// <summary>
    /// The Map Editor's right panel: the selected feature's fields, drawn by Unity's default property fields with the
    /// library key dropdowns, so a new spec field needs no editor code. Each change saves the spec.
    /// </summary>
    [UxmlElement]
    public sealed partial class MapFeatureInspector : VisualElement
    {
        private readonly Label _none;
        private readonly VisualElement _details;
        private readonly Label _title;
        private readonly Label _path;
        private readonly VisualElement _fields;
        private MapSpecDocument _document;

        public MapFeatureInspector()
        {
            EditorAssets.Layout(typeof(MapFeatureInspector)).CloneTree(this);
            _none = this.Require<Label>("none");
            _details = this.Require<VisualElement>("details");
            _title = this.Require<Label>("title");
            _path = this.Require<Label>("path");
            _fields = this.Require<VisualElement>("fields");
            // Raised by a field the user changed, never by the binding catching up with an undo or a reload.
            _fields.RegisterCallback<SerializedPropertyChangeEvent>(_ => _document?.Commit());
        }

        public void Show(MapSpecDocument document, MapFeatureRef feature)
        {
            _document = document;
            _fields.Unbind();
            _fields.Clear();
            _none.SetHidden(!feature.IsNone);
            _details.SetHidden(feature.IsNone);
            if (feature.IsNone)
            {
                return;
            }

            _title.text = feature.ToString();
            _path.text = $"{feature.Kind.Field}[{feature.Index}]";
            var serialized = document.Serialized;
            serialized.Update();
            var element = serialized.FindProperty(feature.PropertyPath);
            var end = element.GetEndProperty();
            var child = element.Copy();
            for (var enter = true; child.NextVisible(enter) && !SerializedProperty.EqualContents(child, end); enter = false)
            {
                _fields.Add(new PropertyField(child.Copy()));
            }

            _fields.Bind(serialized);
        }
    }
}
