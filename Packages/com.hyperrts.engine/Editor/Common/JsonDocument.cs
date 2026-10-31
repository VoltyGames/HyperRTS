using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace HyperRTS.Editor.Common
{
    /// <summary>
    /// A JSON file open for editing, held in a hidden ScriptableObject so edits go through <see cref="Serialized"/>
    /// with Undo and default property fields. The file stays the source: every finished edit, undo and redo saves at
    /// once, changing only what was edited, and a file written from outside reloads when Unity imports it
    /// (<see cref="JsonDocumentImport"/>).
    /// </summary>
    public abstract class JsonDocument : ScriptableObject
    {
        [SerializeField] private string path;

        // The content as last read or written, in the canonical layout, so saving and reloading skip a current file.
        [SerializeField] private string savedJson;

        private SerializedObject _serialized;

        /// <summary>Raised after every edit, undo, redo and load.</summary>
        public event Action Changed;

        public string Path => path;

        public SerializedObject Serialized => _serialized ??= new SerializedObject(this);

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

        /// <summary>Saves edits a bound inspector field applied on its own.</summary>
        public void Commit()
        {
            Save();
            Changed?.Invoke();
        }

        /// <summary>Rereads the file when it holds different content, as after an outside write, dropping the undo history.</summary>
        public void Reload()
        {
            if (File.Exists(path) && OnDisk() != savedJson)
            {
                Load(path);
            }
        }

        /// <summary>A new hidden document of <paramref name="filePath"/>; the caller destroys it when done.</summary>
        protected static T Open<T>(string filePath) where T : JsonDocument
        {
            var document = CreateInstance<T>();
            document.hideFlags = HideFlags.DontSave;
            document.Load(filePath);
            return document;
        }

        /// <summary>Replaces the content with the file's <paramref name="json"/>; throws on a file the format rejects.</summary>
        protected abstract void Read(string json);

        /// <summary>The content in the format's canonical layout.</summary>
        protected abstract string Write();

        /// <summary><paramref name="fileJson"/> with the content written over it, changing only what differs.</summary>
        protected abstract string WriteOver(string fileJson);

        /// <summary><paramref name="fileJson"/> in the canonical layout, to compare with <see cref="Write"/>.</summary>
        protected abstract string Canonical(string fileJson);

        /// <summary>Records an edit made in code rather than through <see cref="Serialized"/> as one undo step, and saves.</summary>
        protected void Record(string undoName, Action edit)
        {
            Undo.RecordObject(this, undoName);
            edit();
            Serialized.Update();
            Save();
            Changed?.Invoke();
        }

        protected virtual void OnEnable() => Undo.undoRedoPerformed += OnUndoRedo;

        protected virtual void OnDisable() => Undo.undoRedoPerformed -= OnUndoRedo;

        private void Load(string filePath)
        {
            var json = File.ReadAllText(filePath);
            Undo.ClearUndo(this);
            path = filePath;
            name = System.IO.Path.GetFileName(filePath);
            Read(json);
            savedJson = Write();
            Serialized.Update();
            Changed?.Invoke();
        }

        private void Save()
        {
            var json = Write();
            if (json != savedJson)
            {
                Store(json);
            }
        }

        // Written over the file as it is now, so an outside write that hasn't reloaded yet is overwritten, not garbled.
        private void Store(string json)
        {
            File.WriteAllText(path, WriteOver(File.ReadAllText(path)));
            savedJson = json;
            AssetDatabase.ImportAsset(path);
        }

        private string OnDisk() => Canonical(File.ReadAllText(path));

        // Undo restores savedJson along with the content, so only the file tells whether it is behind. An undo of
        // something else leaves the two alike and changes nothing.
        private void OnUndoRedo()
        {
            if (!File.Exists(path))
            {
                return;
            }

            Serialized.Update();
            var json = Write();
            if (json == OnDisk())
            {
                savedJson = json;
                return;
            }

            Store(json);
            Changed?.Invoke();
        }
    }
}
