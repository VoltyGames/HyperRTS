using HyperRTS.Editor.Common;
using HyperRTS.Presentation.HUD;
using UnityEngine.UIElements;

namespace HyperRTS.Editor.Maps.Designer
{
    /// <summary>The Map Editor's right panel: where the selected feature sits in the spec, e.g. <c>roads[3]</c>.</summary>
    [UxmlElement]
    public sealed partial class MapFeatureInspector : VisualElement
    {
        private readonly Label _none;
        private readonly VisualElement _details;
        private readonly Label _title;
        private readonly Label _path;

        public MapFeatureInspector()
        {
            EditorAssets.Layout(typeof(MapFeatureInspector)).CloneTree(this);
            _none = this.Require<Label>("none");
            _details = this.Require<VisualElement>("details");
            _title = this.Require<Label>("title");
            _path = this.Require<Label>("path");
        }

        public void Show(MapFeatureRef feature)
        {
            _none.SetHidden(!feature.IsNone);
            _details.SetHidden(feature.IsNone);
            if (feature.IsNone)
            {
                return;
            }

            _title.text = feature.ToString();
            _path.text = $"{feature.Kind.Field}[{feature.Index}]";
        }
    }
}
