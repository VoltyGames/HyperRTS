using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HyperRTS.Editor.Animation;
using HyperRTS.Editor.Common;
using Unity.Pipeline.Commands;
using UnityEditor;

namespace HyperRTS.Editor.Animation
{
    /// <summary>The bone animation bake as a pipeline command and a menu item.</summary>
    public static class BoneCommands
    {
        [CliCommand("bone", "bake", "Bake one bone animation set (a name or its path), or every set when none is named: the bone texture, clip table and meshes written beside it. Returns the asset paths.", Tags = new[] { "hyperrts/animation" })]
        public static object Bake([CliArg("set", "A BoneAnimationSet name or its .asset path; empty bakes every set")] string set = "")
        {
            var sets = string.IsNullOrEmpty(set) ? EditorAssets.FindAssets<BoneAnimationSet>() : new List<BoneAnimationSet> { Find(set) };
            return sets.Select(found => new { set = AssetDatabase.GetAssetPath(found), written = found.Bake() }).ToList();
        }

        [MenuItem("Assets/HyperRTS/Bake Bone Animation Set")]
        private static void BakeSelected()
        {
            foreach (var found in Selection.GetFiltered<BoneAnimationSet>(SelectionMode.Assets))
            {
                found.Bake();
            }
        }

        [MenuItem("Assets/HyperRTS/Bake Bone Animation Set", true)]
        private static bool CanBakeSelected() => Selection.GetFiltered<BoneAnimationSet>(SelectionMode.Assets).Length > 0;

        private static BoneAnimationSet Find(string set)
        {
            var found = EditorAssets.FindAssets<BoneAnimationSet>().Find(candidate => candidate.name == set || AssetDatabase.GetAssetPath(candidate) == set);
            return found != null ? found : throw new FileNotFoundException($"No bone animation set '{set}'.");
        }
    }
}
