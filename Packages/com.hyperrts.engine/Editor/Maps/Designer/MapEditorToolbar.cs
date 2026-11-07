using System;
using HyperRTS.Editor.Common;
using HyperRTS.Presentation.HUD;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace HyperRTS.Editor.Maps.Designer
{
    /// <summary>The Map Editor's toolbar: back to the gallery, the open map's name, the tool strip, the symmetry ghosts toggle and the preview strip.</summary>
    [UxmlElement]
    public sealed partial class MapEditorToolbar : Toolbar
    {
        private readonly Label _name;
        private readonly ToolbarToggle _ghosts;

        public MapEditorToolbar()
        {
            EditorAssets.Layout(typeof(MapEditorToolbar)).CloneTree(this);
            this.Require<ToolbarButton>("maps").clicked += () => GalleryClicked?.Invoke();
            _name = this.Require<Label>("map-name");
            _ghosts = this.Require<ToolbarToggle>("ghosts");
            _ghosts.RegisterValueChangedCallback(evt => GhostsToggled?.Invoke(evt.newValue));
            Tools = this.Require<MapToolStrip>("tools");
            Strip = this.Require<MapPreviewStrip>("strip");
        }

        public event Action GalleryClicked;

        public event Action<bool> GhostsToggled;

        public bool Ghosts => _ghosts.value;

        public MapToolStrip Tools { get; }

        public MapPreviewStrip Strip { get; }

        /// <summary>Shows the open spec's map name.</summary>
        public void Show(MapSpecDocument document) => _name.text = document.MapName;
    }
}
