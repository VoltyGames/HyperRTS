using System;
using System.IO;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace HyperRTS.Editor.Maps.Library
{
    /// <summary>
    /// The Library page's edit form: one item's file loaded into a hidden <see cref="MapLibraryItemWrapper"/> and
    /// shown as property fields, with Save, Revert and a marker while it differs from the file.
    /// </summary>
    public sealed class LibraryItemForm : VisualElement
    {
        private readonly Label _title = new();
        private readonly Label _dirty = new("Unsaved changes");
        private readonly VisualElement _buttons = new();
        private readonly VisualElement _fields = new();
        private MapLibraryItemWrapper _wrapper;
        private SerializedObject _serialized;
        private string _saved;

        public LibraryItemForm()
        {
            AddToClassList("hrts-library-form");
            var header = new VisualElement();
            header.AddToClassList("hrts-library-form__header");
            _title.AddToClassList("hrts-library-form__title");
            header.Add(_title);
            _dirty.AddToClassList("hrts-library-form__dirty");
            header.Add(_dirty);
            _buttons.AddToClassList("hrts-buttons");
            _buttons.Add(new Button(Save) { text = "Save" });
            _buttons.Add(new Button(Revert) { text = "Revert" });
            header.Add(_buttons);
            Add(header);
            _fields.AddToClassList("hrts-library-form__fields");
            Add(_fields);
            Close();
        }

        public Type Type { get; private set; }

        public string Key { get; private set; }

        public string File { get; private set; }

        /// <summary>The folder of the open item's file, for New and Duplicate to start in.</summary>
        public string Folder => File == null ? "Assets" : Path.GetDirectoryName(File).Replace('\\', '/');

        /// <summary>The fields differ from what the file held when it was opened or last saved.</summary>
        public bool IsDirty => _wrapper != null && Json() != _saved;

        public void Open(Type type, string key, string file)
        {
            Close();
            var (item, refs) = MapLibraryItemFile.Read(type, file);
            (Type, Key, File) = (type, key, file);
            _wrapper = ScriptableObject.CreateInstance<MapLibraryItemWrapper>();
            _wrapper.hideFlags = HideFlags.DontSave;
            _wrapper.name = $"{key}.{MapLibrary.TypeName(type)}";
            (_wrapper.item, _wrapper.refs) = (item, refs);
            _saved = Json();
            _serialized = new SerializedObject(_wrapper);
            AddFields();
            _title.text = $"{key} · {MapLibrary.TypeName(type)}";
            _title.tooltip = file;
            _buttons.style.display = DisplayStyle.Flex;
            UpdateDirty();
        }

        /// <summary>Shows nothing and drops the wrapper (and its Undo history).</summary>
        public void Close()
        {
            _fields.Unbind();
            _fields.Clear();
            if (_wrapper != null)
            {
                Undo.ClearUndo(_wrapper);
                Object.DestroyImmediate(_wrapper);
            }

            (_wrapper, _serialized, _saved) = (null, null, null);
            (Type, Key, File) = (null, null, null);
            _title.text = "Pick an item";
            _buttons.style.display = DisplayStyle.None;
            _dirty.style.display = DisplayStyle.None;
        }

        public void Save()
        {
            if (_wrapper == null)
            {
                return;
            }

            _serialized.ApplyModifiedProperties();
            var json = Json();
            System.IO.File.WriteAllText(File, json);
            _saved = json;
            AssetDatabase.ImportAsset(File);
            UpdateDirty();
        }

        public void Revert()
        {
            if (_wrapper != null)
            {
                Open(Type, Key, File);
            }
        }

        private void AddFields()
        {
            foreach (var name in MapLibraryItemAttribute.Require(Type).Refs)
            {
                AddField(_serialized.FindProperty($"{nameof(MapLibraryItemWrapper.refs)}.{name}"));
            }

            var item = _serialized.FindProperty(nameof(MapLibraryItemWrapper.item));
            var end = item.GetEndProperty();
            var child = item.Copy();
            for (var more = child.NextVisible(true); more && !SerializedProperty.EqualContents(child, end);
                 more = child.NextVisible(false))
            {
                AddField(child.Copy());
            }

            _fields.Bind(_serialized);
            _fields.TrackSerializedObjectValue(_serialized, _ => UpdateDirty());
        }

        private void AddField(SerializedProperty property) => _fields.Add(new PropertyField(property));

        private string Json() => MapLibraryItemFile.ToJson(Type, _wrapper.item, _wrapper.refs);

        private void UpdateDirty() =>
            _dirty.style.display = IsDirty ? DisplayStyle.Flex : DisplayStyle.None;
    }
}
