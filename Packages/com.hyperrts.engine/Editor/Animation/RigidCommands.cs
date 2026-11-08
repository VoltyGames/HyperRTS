using System.Collections.Generic;
using System.IO;
using System.Linq;
using HyperRTS.Editor.Common;
using Unity.Pipeline.Commands;
using UnityEditor;

namespace HyperRTS.Editor.Animation
{
    /// <summary>The rigid animation bake as a pipeline command and a menu item.</summary>
    public static class RigidCommands
    {
        [CliCommand("rigid", "bake", "Bake one rigid animation set (a name or its path), or every set when none is named: the position and rotation textures and the clip table. Returns the asset paths.", Tags = new[] { "hyperrts/animation" })]
        public static object Bake([CliArg("set", "A RigidAnimationSet name or its .asset path; empty bakes every set")] string set = "")
        {
            var sets = string.IsNullOrEmpty(set) ? EditorAssets.FindAssets<RigidAnimationSet>() : new List<RigidAnimationSet> { Find(set) };
            return sets.Select(found => new { set = AssetDatabase.GetAssetPath(found), written = found.Bake() }).ToList();
        }

        [MenuItem("Assets/HyperRTS/Bake Rigid Animation Set")]
        private static void BakeSelected()
        {
            foreach (var found in Selection.GetFiltered<RigidAnimationSet>(SelectionMode.Assets))
            {
                found.Bake();
            }
        }

        [MenuItem("Assets/HyperRTS/Bake Rigid Animation Set", true)]
        private static bool CanBakeSelected() => Selection.GetFiltered<RigidAnimationSet>(SelectionMode.Assets).Length > 0;

        private static RigidAnimationSet Find(string set)
        {
            var found = EditorAssets.FindAssets<RigidAnimationSet>().Find(candidate => candidate.name == set || AssetDatabase.GetAssetPath(candidate) == set);
            return found != null ? found : throw new FileNotFoundException($"No rigid animation set '{set}'.");
        }
    }
}
