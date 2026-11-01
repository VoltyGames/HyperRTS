using System;
using System.Collections;
using HyperRTS.Editor.Common;
using HyperRTS.Simulation.Selection;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HyperRTS.Editor.Validation
{
    /// <summary>One-click fixes offered next to validation issues; each is a single undo step.</summary>
    public static class QuickFixes
    {
        /// <summary>Adds a pick box on the root, sized to the renderers below it.</summary>
        public static void FitPickBox(Component root) =>
            Undo.AddComponent<PickBoxAuthoring>(root.gameObject).FitToRenderers();

        /// <summary>Swaps a scene instance in an option list for the prefab asset it came from.</summary>
        public static void UsePrefab(Object owner, IList options, Object instance) =>
            EditorUndo.Record(owner, "Use Prefab", () =>
            {
                var index = options.IndexOf(instance);
                var source = PrefabUtility.GetCorrespondingObjectFromSource(instance);
                if (index >= 0 && source != null)
                {
                    options[index] = source;
                }
            });

        public static void RemoveEmpty(Object owner, IList list, Predicate<object> isEmpty) =>
            EditorUndo.Record(owner, "Remove Empty Entries", () =>
            {
                for (var i = list.Count - 1; i >= 0; i--)
                {
                    if (isEmpty(list[i]))
                    {
                        list.RemoveAt(i);
                    }
                }
            });
    }
}
