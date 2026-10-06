using System;
using System.Collections.Generic;
using HyperRTS.Editor.Common;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HyperRTS.Editor.Validation
{
    /// <summary>One-click fixes offered next to validation issues; each is a single undo step.</summary>
    public static class QuickFixes
    {
        /// <summary>Adds a box collider on the root, sized to the renderers below it.</summary>
        public static void FitCollider(Component root)
        {
            var renderers = root.GetComponentsInChildren<Renderer>();
            var collider = Undo.AddComponent<BoxCollider>(root.gameObject);
            if (renderers.Length == 0)
            {
                return;
            }

            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers)
            {
                bounds.Encapsulate(renderer.bounds);
            }

            var scale = root.transform.lossyScale;
            collider.center = root.transform.InverseTransformPoint(bounds.center);
            collider.size = new Vector3(bounds.size.x / scale.x, bounds.size.y / scale.y, bounds.size.z / scale.z);
        }

        public static Component AddComponent(GameObject go, Type type) => Undo.AddComponent(go, type);

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
