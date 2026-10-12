using System;
using System.IO;
using HyperRTS.Editor.Maps.Generation;
using HyperRTS.Editor.Maps.Spec;
using UnityEditor;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Designer
{
    /// <summary>
    /// A map spec open in the Map Editor. Every edit goes through <see cref="Serialized"/>, so Undo and Redo work;
    /// the document is dirty while its spec writes differently from the file it was loaded from or saved to.
    /// </summary>
    public sealed class MapSpecDocument : ScriptableObject
    {
        public const string SpecField = nameof(spec);

        public MapSpec spec = new();

        [SerializeField] private string path;
        [SerializeField] private string savedJson;
        [SerializeField] private long diskTicks;

        private SerializedObject _serialized;
        private bool _dirtyKnown;
        private bool _dirty;

        /// <summary>Raised after any edit, undo, redo, load or save.</summary>
        public event Action Changed;

        public string Path => path;

        public string MapName => MapPaths.MapName(path);

        public SerializedObject Serialized => _serialized ??= new SerializedObject(this);

        public bool IsDirty
        {
            get
            {
                if (!_dirtyKnown)
                {
                    _dirty = MapSpecJson.Write(spec) != savedJson;
                    _dirtyKnown = true;
                }

                return _dirty;
            }
        }

        public static MapSpecDocument Open(string specPath)
        {
            var document = CreateInstance<MapSpecDocument>();
            document.hideFlags = HideFlags.DontSave;
            document.Load(specPath);
            return document;
        }

        /// <summary>Reads the spec from disk, dropping unsaved edits and the undo history.</summary>
        public void Load(string specPath)
        {
            if (!MapPaths.IsSpec(specPath))
            {
                throw new ArgumentException($"'{specPath}' is not a map spec ({MapPaths.SpecExtension}).");
            }

            var loaded = MapSpecJson.Read(File.ReadAllText(specPath));
            Undo.ClearUndo(this);
            path = specPath;
            name = MapPaths.MapName(specPath);
            spec = loaded;
            savedJson = MapSpecJson.Write(spec);
            diskTicks = File.GetLastWriteTimeUtc(path).Ticks;
            Serialized.Update();
            NotifyChanged();
        }

        public void Revert() => Load(path);

        public void Save()
        {
            var json = MapSpecJson.Write(spec);
            File.WriteAllText(path, json);
            savedJson = json;
            diskTicks = File.GetLastWriteTimeUtc(path).Ticks;
            AssetDatabase.ImportAsset(path);
            NotifyChanged();
        }

        /// <summary>Reloads when the file changed on disk (an agent edited it) and nothing here is unsaved.</summary>
        public bool ReloadIfChangedOnDisk()
        {
            if (!File.Exists(path) || File.GetLastWriteTimeUtc(path).Ticks == diskTicks || IsDirty)
            {
                return false;
            }

            Load(path);
            return true;
        }

        /// <summary>Writes pending <see cref="Serialized"/> edits as one undo step and tells the views.</summary>
        public void Apply(string undoName)
        {
            if (Serialized.ApplyModifiedProperties())
            {
                Undo.SetCurrentGroupName(undoName);
            }

            NotifyChanged();
        }

        public void NotifyChanged()
        {
            _dirtyKnown = false;
            Changed?.Invoke();
        }

        private void OnEnable() => Undo.undoRedoPerformed += OnUndoRedo;

        private void OnDisable() => Undo.undoRedoPerformed -= OnUndoRedo;

        private void OnUndoRedo()
        {
            Serialized.Update();
            NotifyChanged();
        }
    }
}
