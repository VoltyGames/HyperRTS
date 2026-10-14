using System;
using System.Linq;
using HyperRTS.Editor.Maps.Generation;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace HyperRTS.Editor.Maps.Designer
{
    /// <summary>The Map Editor's toolbar: spec picker and the symmetry ghosts toggle.</summary>
    public sealed class MapEditorToolbar : Toolbar
    {
        private readonly PopupField<string> _picker;
        private readonly ToolbarToggle _ghosts;

        public MapEditorToolbar(Action<string> pick, Action<bool> ghosts)
        {
            var specs = MapPaths.All().OrderBy(MapPaths.MapName).ToList();
            _picker = new PopupField<string>(specs, -1, Label, Label);
            _picker.AddToClassList("hrts-map-toolbar__picker");
            _picker.RegisterValueChangedCallback(evt => pick(evt.newValue));
            Add(_picker);

            var spacer = new ToolbarSpacer();
            spacer.AddToClassList("hrts-fill");
            Add(spacer);

            _ghosts = new ToolbarToggle { text = "Ghosts", value = true };
            _ghosts.RegisterValueChangedCallback(evt => ghosts(evt.newValue));
            Add(_ghosts);
        }

        public bool Ghosts => _ghosts.value;

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
