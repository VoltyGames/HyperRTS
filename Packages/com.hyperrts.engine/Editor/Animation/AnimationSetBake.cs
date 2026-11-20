using System.Collections.Generic;
using System.IO;
using System.Linq;
using HyperRTS.Editor.Common;
using UnityEditor;
using UnityEngine;

namespace HyperRTS.Editor.Animation
{
    /// <summary>What the bake commands and menu items share: baking one named set, every set or the selection.</summary>
    public static class AnimationSetBake
    {
        /// <summary>Bakes the set named or at a path, or every set when the name is empty; returns each set with what it wrote.</summary>
        public static object Bake<T>(string set) where T : ScriptableObject, IAnimationSet
        {
            var sets = string.IsNullOrEmpty(set) ? EditorAssets.FindAssets<T>() : new List<T> { Find<T>(set) };
            return sets.Select(found => new { set = AssetDatabase.GetAssetPath(found), written = found.Bake() }).ToList();
        }

        /// <summary>Bakes every set of this type selected in the Project window.</summary>
        public static void BakeSelected<T>() where T : Object, IAnimationSet
        {
            foreach (var found in Selection.GetFiltered<T>(SelectionMode.Assets))
            {
                found.Bake();
            }
        }

        /// <summary>Whether the Project window selection holds a set of this type.</summary>
        public static bool HasSelected<T>() where T : Object => Selection.GetFiltered<T>(SelectionMode.Assets).Length > 0;

        private static T Find<T>(string set) where T : ScriptableObject
        {
            return EditorAssets.FindAssets<T>().Find(candidate => candidate.name == set || AssetDatabase.GetAssetPath(candidate) == set)
                ?? throw new FileNotFoundException($"No {typeof(T).Name} '{set}'.");
        }
    }
}
