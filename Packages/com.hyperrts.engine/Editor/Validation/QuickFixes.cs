using System;
using System.Collections.Generic;
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
        public static void UsePrefab<T>(Object owner, List<T> options, T instance) where T : Object =>
            EditorUndo.Record(owner, "Use Prefab", () =>
            {
                var index = options.IndexOf(instance);
                var source = PrefabUtility.GetCorrespondingObjectFromSource(instance);
                if (index >= 0 && source != null)
                {
                    options[index] = source;
                }
            });

        public static void RemoveEmpty<T>(Object owner, List<T> list, Predicate<T> isEmpty) =>
            EditorUndo.Record(owner, "Remove Empty Entries", () => list.RemoveAll(isEmpty));
    }
}
