using System;
using System.Linq;
using HyperRTS.Editor.Maps.Generation;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace HyperRTS.Editor.Maps.Designer
{
    /// <summary>The Map Editor's toolbar: spec picker, Save, Revert and the symmetry ghosts toggle.</summary>
    public sealed class MapEditorToolbar : Toolbar
    {
        private readonly PopupField<string> _picker;
        private readonly ToolbarButton _save;
        private readonly ToolbarButton _revert;
        private readonly ToolbarToggle _ghosts;

        public MapEditorToolbar(Action<string> pick, Action save, Action revert, Action<bool> ghosts)
        {
            var specs = MapPaths.All().OrderBy(MapPaths.NameOf).ToList();
            _picker = new PopupField<string>(specs, -1, MapPaths.NameOf, MapPaths.NameOf);
            _picker.AddToClassList("hrts-map-toolbar__picker");
            _picker.RegisterValueChangedCallback(evt => pick(evt.newValue));
            Add(_picker);

            _save = new ToolbarButton(save) { text = "Save" };
            Add(_save);
            _revert = new ToolbarButton(revert) { text = "Revert" };
            Add(_revert);

            var spacer = new ToolbarSpacer();
            spacer.AddToClassList("hrts-fill");
            Add(spacer);

            _ghosts = new ToolbarToggle { text = "Ghosts", value = true };
            _ghosts.RegisterValueChangedCallback(evt => ghosts(evt.newValue));
            Add(_ghosts);
        }

        public bool Ghosts => _ghosts.value;

        /// <summary>Shows the open spec in the picker and enables Save and Revert while it has unsaved edits.</summary>
        public void Show(MapSpecDocument document)
        {
            _save.SetEnabled(document.IsDirty);
            _revert.SetEnabled(document.IsDirty);
            if (!_picker.choices.Contains(document.Path))
            {
                _picker.choices.Add(document.Path);
            }

            _picker.SetValueWithoutNotify(document.Path);
        }

        /// <summary>No spec open yet: nothing to save or revert.</summary>
        public void ShowNone()
        {
            _save.SetEnabled(false);
            _revert.SetEnabled(false);
        }
    }
}
