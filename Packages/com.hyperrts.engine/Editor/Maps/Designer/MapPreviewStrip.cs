using System;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace HyperRTS.Editor.Maps.Designer
{
    /// <summary>The Map Editor toolbar's preview and build controls: Auto preview, Preview, a busy note and Build.</summary>
    public sealed class MapPreviewStrip : VisualElement
    {
        private readonly Label _busy;

        public MapPreviewStrip(Action preview, Action build)
        {
            AddToClassList("hrts-map-toolbar__strip");
            var auto = new ToolbarToggle
            {
                text = "Auto preview",
                value = MapLivePreview.Auto,
                tooltip = "Preview the map when it opens and whenever its spec file changes.",
            };
            auto.RegisterValueChangedCallback(change => MapLivePreview.Auto = change.newValue);
            Add(auto);
            Add(new ToolbarButton(preview) { text = "Preview", tooltip = "Preview the map as it is now." });

            _busy = new Label("Previewing...");
            _busy.AddToClassList("hrts-toolbar-label");
            _busy.AddToClassList("hrts-map-toolbar__busy");
            Add(_busy);
            ShowBusy(false);

            Add(new ToolbarButton(build) { text = "Build", tooltip = "Build the map's scene and show the report." });
        }

        public void ShowBusy(bool busy) => _busy.style.visibility = busy ? Visibility.Visible : Visibility.Hidden;
    }
}
