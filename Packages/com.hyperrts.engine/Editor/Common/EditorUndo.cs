using System;
using UnityEditor;
using UnityEngine;
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

        /// <summary>Records an undoable change to anything under <paramref name="root"/>, such as a fill that sets its children.</summary>
        public static void RecordHierarchy(GameObject root, string label, Action change)
        {
            Undo.RegisterFullObjectHierarchyUndo(root, label);
            change();
            foreach (var component in root.GetComponentsInChildren<Component>(true))
            {
                EditorUtility.SetDirty(component);
                PrefabUtility.RecordPrefabInstancePropertyModifications(component);
            }
        }
    }
}
