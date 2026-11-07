using System.Collections.Generic;
using System.Linq;
using HyperRTS.Presentation.Effects.Rig;
using UnityEditor;
using UnityEngine;

namespace HyperRTS.Editor.Effects.Preview
{
    /// <summary>An effect rig prefab in the project, with the kind it is listed under and the recipes of its graphs.</summary>
    public sealed class EffectRigEntry
    {
        public EffectRigEntry(string path, List<string> recipes)
        {
            Path = path;
            Recipes = recipes;
        }

        public string Path { get; }

        public string Name => System.IO.Path.GetFileNameWithoutExtension(Path);

        public string Kind => KindOf(Path);

        public List<string> Recipes { get; }

        /// <summary>The second field of an <c>FX_&lt;Kind&gt;_...</c> name, or the prefab's folder name for any other name.</summary>
        public static string KindOf(string path)
        {
            var fields = System.IO.Path.GetFileNameWithoutExtension(path).Split('_');
            if (fields[0] == "FX" && fields.Length > 1)
            {
                return fields[1];
            }

            return System.IO.Path.GetFileName(System.IO.Path.GetDirectoryName(path));
        }

        /// <summary>Every prefab in the project whose root has an <see cref="EffectRig"/>, by kind then name.</summary>
        public static List<EffectRigEntry> Find()
        {
            var script = MonoImporter.GetAllRuntimeMonoScripts().First(candidate => candidate.GetClass() == typeof(EffectRig));
            var scriptPath = AssetDatabase.GetAssetPath(script);
            var folders = new[] { "Assets", "Packages/com.hyperrts.engine" };
            var entries = new List<EffectRigEntry>();
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", folders))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (!AssetDatabase.GetDependencies(path, true).Contains(scriptPath))
                {
                    continue;
                }

                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab != null && prefab.TryGetComponent<EffectRig>(out _))
                {
                    entries.Add(new EffectRigEntry(path, EffectGraphBuilder.RecipesFor(prefab)));
                }
            }

            return entries.OrderBy(entry => entry.Kind).ThenBy(entry => entry.Name).ToList();
        }
    }
}
