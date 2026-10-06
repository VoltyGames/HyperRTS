using System;
using UnityEditor;
using Object = UnityEngine.Object;

namespace HyperRTS.Editor.Common
{
    /// <summary>Undoable edits to serialized objects, shared by inspectors, handles and quick fixes.</summary>
    public static class EditorUndo
    {
        /// <summary>Records an undoable change, keeping prefab-instance overrides.</summary>
        public static void Record(Object target, string label, Action change)
        {
            Undo.RecordObject(target, label);
            change();
            EditorUtility.SetDirty(target);
            PrefabUtility.RecordPrefabInstancePropertyModifications(target);
        }
    }
}
