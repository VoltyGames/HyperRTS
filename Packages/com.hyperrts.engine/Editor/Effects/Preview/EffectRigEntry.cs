using System.Collections.Generic;
using System.Linq;
using HyperRTS.Editor.Common;
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
            Kind = ReadKind(path);
            Recipes = recipes;
        }

        public string Path { get; }

        public string Name => System.IO.Path.GetFileNameWithoutExtension(Path);

        /// <summary>The second field of an <c>FX_&lt;Kind&gt;_...</c> name, or the prefab's folder name for any other name.</summary>
        public string Kind { get; }

        public List<string> Recipes { get; }

        private static string ReadKind(string path)
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
            var entries = new List<EffectRigEntry>();
            foreach (var path in EditorAssets.PrefabsUsing(type => type == typeof(EffectRig), "Assets", EnginePackage.Folder))
            {
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
