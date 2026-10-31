using System;
using System.Collections.Generic;
using System.Linq;
using HyperRTS.Editor.Common;
using UnityEditor;
using UnityEngine;

namespace HyperRTS.Editor.Generators
{
    /// <summary>Every available <see cref="Generator"/> in run order, and running them.</summary>
    public static class GeneratorRunner
    {
        public static IReadOnlyList<Generator> All()
        {
            var all = TypeDiscovery.Instances<Generator>()
                .Where(generator => generator.Available)
                .OrderBy(generator => generator.Order)
                .ThenBy(generator => generator.Name)
                .ToList();
            var duplicate = all.GroupBy(generator => generator.Name).FirstOrDefault(group => group.Count() > 1);
            if (duplicate != null)
            {
                throw new InvalidOperationException($"HyperRTS: two generators are named '{duplicate.Key}'.");
            }

            return all;
        }

        /// <summary>The available generator with this name; throws listing the names when there is none.</summary>
        public static Generator Find(string name)
        {
            var all = All();
            return all.FirstOrDefault(generator => generator.Name == name)
                ?? throw new ArgumentException($"HyperRTS: no generator is named '{name}'. Generators: {string.Join(", ", all.Select(generator => generator.Name))}.");
        }

        /// <summary>Runs every generator in order; also the batch-mode entry (-executeMethod).</summary>
        public static void RunAll()
        {
            var all = All();
            try
            {
                for (var i = 0; i < all.Count; i++)
                {
                    EditorUtility.DisplayProgressBar("HyperRTS", all[i].Name, (float)i / all.Count);
                    Run(all[i]);
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            Debug.Log($"HyperRTS: ran {all.Count} generators.");
        }

        public static void Run(Generator generator)
        {
            generator.Run();
            AssetDatabase.SaveAssets();
        }
    }
}
