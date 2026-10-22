using System;
using System.Linq;
using HyperRTS.Editor.Common;
using HyperRTS.Editor.Maps.Generation;
using HyperRTS.Presentation.HUD;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace HyperRTS.Editor.Maps.Designer
{
    /// <summary>The Map Editor's toolbar: spec picker, the symmetry ghosts toggle and the preview strip.</summary>
    [UxmlElement]
    public sealed partial class MapEditorToolbar : Toolbar
    {
        private readonly DropdownField _picker;
        private readonly ToolbarToggle _ghosts;

        public MapEditorToolbar()
        {
            EditorAssets.Layout(typeof(MapEditorToolbar)).CloneTree(this);
            _picker = this.Require<DropdownField>("picker");
            _picker.choices = MapPaths.All().OrderBy(MapPaths.MapName).ToList();
            _picker.formatSelectedValueCallback = Label;
            _picker.formatListItemCallback = Label;
            _picker.SetValueWithoutNotify(null);
            _picker.RegisterValueChangedCallback(evt => SpecPicked?.Invoke(evt.newValue));

            _ghosts = this.Require<ToolbarToggle>("ghosts");
            _ghosts.RegisterValueChangedCallback(evt => GhostsToggled?.Invoke(evt.newValue));
            Strip = this.Require<MapPreviewStrip>("strip");
        }

        public event Action<string> SpecPicked;

        public event Action<bool> GhostsToggled;

        public bool Ghosts => _ghosts.value;

        public MapPreviewStrip Strip { get; }

        /// <summary>Shows the open spec in the picker.</summary>
        public void Show(MapSpecDocument document)
        {
            if (!_picker.choices.Contains(document.Path))
            {
                _picker.choices.Add(document.Path);
            }

            _picker.SetValueWithoutNotify(document.Path);
        }

        // The picker formats its empty value too, before any spec is open.
        private static string Label(string spec) => spec == null ? "Open a map…" : MapPaths.MapName(spec);
    }
}
