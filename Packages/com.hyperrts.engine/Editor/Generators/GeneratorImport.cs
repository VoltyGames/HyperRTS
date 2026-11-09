using System.Collections.Generic;
using System.Linq;
using Unity.Scripting.LifecycleManagement;
using UnityEditor;

namespace HyperRTS.Editor.Generators
{
    /// <summary>Runs each <see cref="Generator"/> that has <see cref="Generator.RunOnImport"/> once after import batches that touched something it reads.</summary>
    public sealed class GeneratorImport : AssetPostprocessor
    {
        [NoAutoStaticsCleanup]
        private static readonly HashSet<string> Changed = new();

        private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            var read = imported.Concat(deleted).Concat(moved).Concat(movedFrom)
                .Where(path => GeneratorRunner.All().Any(generator => generator.RunOnImport && generator.Reads(path)))
                .ToList();
            if (read.Count == 0)
            {
                return;
            }

            // After the batch, so every changed asset has loaded; batches before the run join it.
            if (Changed.Count == 0)
            {
                EditorApplication.delayCall += RunChanged;
            }

            Changed.UnionWith(read);
        }

        private static void RunChanged()
        {
            var changed = Changed.ToList();
            Changed.Clear();
            foreach (var generator in GeneratorRunner.All().Where(generator => generator.RunOnImport && changed.Any(generator.Reads)))
            {
                GeneratorRunner.Run(generator);
            }
        }
    }
}
