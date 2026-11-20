using System;
using System.IO;
using HyperRTS.Editor.Maps.Build;
using HyperRTS.Editor.Maps.Spec;
using UnityEditor;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Designer.Window
{
    /// <summary>
    /// A map spec open in the Map Editor through <see cref="Serialized"/> with Undo. Every edit saves at once in the
    /// canonical layout, and a file written from outside reloads (<see cref="MapSpecImport"/>).
    /// </summary>
    public sealed class MapSpecDocument : ScriptableObject
    {
        /// <summary>The serialized field holding the spec, the root of every property path (<c>spec.roads.Array.data[3]</c>).</summary>
        public const string SpecField = nameof(spec);

        [SerializeField] private string path;

        // The file content as last read or written, in the canonical layout, so saving and reloading skip a current file.
        [SerializeField] private string savedJson;

        [SerializeField] private MapSpec spec = new();

        private SerializedObject _serialized;

        /// <summary>Raised after every edit, undo, redo and load.</summary>
        public event Action Changed;

        public string Path => path;

        public string MapName => MapPaths.MapName(Path);

        public MapSpec Spec => spec;

        public SerializedObject Serialized => _serialized ??= new SerializedObject(this);

        /// <summary>The document for <paramref name="specPath"/>; the caller destroys it when done.</summary>
        public static MapSpecDocument Open(string specPath)
        {
            if (!MapPaths.IsSpec(specPath))
            {
                throw new ArgumentException($"'{specPath}' is not a map spec ({MapPaths.SpecExtension}).");
            }

            var document = CreateInstance<MapSpecDocument>();
            document.hideFlags = HideFlags.DontSave;
            document.Load(specPath, ReadFile(specPath));
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
                Commit();
                return;
            }

            Changed?.Invoke();
        }

        /// <summary>Saves edits a bound inspector field applied on its own.</summary>
        public void Commit()
        {
            Save();
            Changed?.Invoke();
        }

        /// <summary>Records <paramref name="edit"/> to the spec, made in code rather than through the serialized object, as one undo step, and saves.</summary>
        public void Edit(string undoName, Action<MapSpec> edit)
        {
            Undo.RecordObject(this, undoName);
            edit(spec);
            Serialized.Update();
            Commit();
        }

        /// <summary>Rereads the file when it holds different content, as after an outside write, dropping the undo history.</summary>
        public void Reload()
        {
            if (!File.Exists(path))
            {
                return;
            }

            var file = ReadFile(path);
            if (file.Json != savedJson)
            {
                Load(path, file);
            }
        }

        private void OnEnable() => Undo.undoRedoPerformed += OnUndoRedo;

        private void OnDisable() => Undo.undoRedoPerformed -= OnUndoRedo;

        // The file's spec and its content in the canonical layout.
        private static (MapSpec Spec, string Json) ReadFile(string filePath)
        {
            var read = MapSpecJson.Read(File.ReadAllText(filePath));
            return (read, MapSpecJson.Write(read));
        }

        private void Load(string filePath, (MapSpec Spec, string Json) file)
        {
            Undo.ClearUndo(this);
            path = filePath;
            name = System.IO.Path.GetFileName(filePath);
            spec = file.Spec;
            savedJson = file.Json;
            Serialized.Update();
            Changed?.Invoke();
        }

        private void Save()
        {
            var json = MapSpecJson.Write(spec);
            if (json != savedJson)
            {
                Store(json);
            }
        }

        private void Store(string json)
        {
            File.WriteAllText(path, json);
            savedJson = json;
            AssetDatabase.ImportAsset(path);
        }

        // Undo restores savedJson along with the content, so only the file tells whether it is behind. An undo of
        // something else leaves the two alike and changes nothing.
        private void OnUndoRedo()
        {
            if (!File.Exists(path))
            {
                return;
            }

            Serialized.Update();
            var json = MapSpecJson.Write(spec);
            if (json == ReadFile(path).Json)
            {
                savedJson = json;
                return;
            }

            Store(json);
            Changed?.Invoke();
        }
    }
}
