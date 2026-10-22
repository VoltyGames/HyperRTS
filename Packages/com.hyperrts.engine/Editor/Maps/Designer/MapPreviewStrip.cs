using System;
using HyperRTS.Editor.Common;
using HyperRTS.Presentation.HUD;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace HyperRTS.Editor.Maps.Designer
{
    /// <summary>The Map Editor toolbar's preview and build controls: Auto preview, Preview, a busy note and Build.</summary>
    [UxmlElement]
    public sealed partial class MapPreviewStrip : VisualElement
    {
        private readonly Label _busy;

        public MapPreviewStrip()
        {
            EditorAssets.Layout(typeof(MapPreviewStrip)).CloneTree(this);
            var auto = this.Require<ToolbarToggle>("auto");
            auto.value = MapLivePreview.Auto;
            auto.RegisterValueChangedCallback(change => MapLivePreview.Auto = change.newValue);
            this.Require<ToolbarButton>("preview").clicked += () => PreviewClicked?.Invoke();
            this.Require<ToolbarButton>("build").clicked += () => BuildClicked?.Invoke();
            _busy = this.Require<Label>("busy");
        }

        public event Action PreviewClicked;

        public event Action BuildClicked;

        public void ShowBusy(bool busy) => _busy.SetInvisible(!busy);
    }
}
