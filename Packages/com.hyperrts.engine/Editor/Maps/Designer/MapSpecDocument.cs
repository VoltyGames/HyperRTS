using System;
using System.IO;
using HyperRTS.Editor.Maps.Generation;
using HyperRTS.Editor.Maps.Spec;
using UnityEditor;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Designer
{
    /// <summary>
    /// A map spec open in the Map Editor, held in a hidden ScriptableObject so edits go through <see cref="Serialized"/>
    /// with Undo. The file stays the source: every finished edit, undo and redo saves at once, so nothing is ever
    /// unsaved, and a file written from outside (an agent, a text editor) reloads when Unity imports it.
    /// </summary>
    public sealed class MapSpecDocument : ScriptableObject
    {
        /// <summary>The serialized field holding the spec, the root of every property path (<c>spec.roads.Array.data[3]</c>).</summary>
        public const string SpecField = nameof(spec);

        [SerializeField] private MapSpec spec = new();
        [SerializeField] private string path;

        // The spec as last read or written, in MapSpecJson's layout, so saving and reloading skip a file that is current.
        [SerializeField] private string savedJson;

        private SerializedObject _serialized;

        /// <summary>Raised after every edit, undo, redo and load.</summary>
        public event Action Changed;

        public string Path => path;

        public string MapName => MapPaths.MapName(path);

        public MapSpec Spec => spec;

        public SerializedObject Serialized => _serialized ??= new SerializedObject(this);

        /// <summary>The document for <paramref name="specPath"/>; the caller destroys it when done.</summary>
        public static MapSpecDocument Open(string specPath)
        {
            var document = CreateInstance<MapSpecDocument>();
            document.hideFlags = HideFlags.DontSave;
            document.Load(specPath);
            return document;
        }

        /// <summary>The property path of an element of a spec list, such as <c>spec.roads.Array.data[3]</c>.</summary>
        public static string ElementPath(string list, int index) => $"{SpecField}.{list}.Array.data[{index}]";

        /// <summary>
        /// Writes the pending <see cref="Serialized"/> edits as one undo step named <paramref name="undoName"/>. A
        /// finished edit also saves; a drag in progress passes false and saves once it ends.
        /// </summary>
        public void Apply(string undoName, bool finished = true)
        {
            if (Serialized.ApplyModifiedProperties())
            {
                Undo.SetCurrentGroupName(undoName);
            }

            if (finished)
            {
                Save();
            }

            Changed?.Invoke();
        }

        /// <summary>Records <paramref name="edit"/> to the spec, made in code rather than through <see cref="Serialized"/>, as one undo step, and saves.</summary>
        public void Edit(string undoName, Action<MapSpec> edit)
        {
            Undo.RecordObject(this, undoName);
            edit(spec);
            Serialized.Update();
            Save();
            Changed?.Invoke();
        }

        /// <summary>Saves edits a bound inspector field applied on its own.</summary>
        public void Commit()
        {
            Save();
            Changed?.Invoke();
        }

        /// <summary>Rereads the file when it holds a different spec, as after an outside write, dropping the undo history.</summary>
        public void Reload()
        {
            if (OnDisk() != savedJson)
            {
                Load(path);
            }
        }

        private void Load(string specPath)
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
            Serialized.Update();
            Changed?.Invoke();
        }

        private void Save()
        {
            var json = MapSpecJson.Write(spec);
            if (json != savedJson)
            {
                Write(json);
            }
        }

        // Written over the file as it is now, so an outside write that hasn't reloaded yet is overwritten, not garbled.
        private void Write(string json)
        {
            File.WriteAllText(path, MapSpecJson.WriteOver(File.ReadAllText(path), spec));
            savedJson = json;
            AssetDatabase.ImportAsset(path);
        }

        private string OnDisk() => MapSpecJson.Write(MapSpecJson.Read(File.ReadAllText(path)));

        private void OnEnable() => Undo.undoRedoPerformed += OnUndoRedo;

        private void OnDisable() => Undo.undoRedoPerformed -= OnUndoRedo;

        // Undo restores savedJson along with the spec, so only the file tells whether it is behind. An undo of
        // something else leaves the two alike and changes nothing.
        private void OnUndoRedo()
        {
            if (!File.Exists(path))
            {
                return;
            }

            Serialized.Update();
            var json = MapSpecJson.Write(spec);
            if (json == OnDisk())
            {
                savedJson = json;
                return;
            }

            Write(json);
            Changed?.Invoke();
        }
    }
}
