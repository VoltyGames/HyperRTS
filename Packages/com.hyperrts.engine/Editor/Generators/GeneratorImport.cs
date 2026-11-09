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
            var paths = imported.Concat(deleted).Concat(moved).Concat(movedFrom).ToList();
            var read = GeneratorRunner.All()
                .Where(generator => generator.RunOnImport && paths.Any(generator.Reads))
                .Select(generator => generator.Name)
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
            var changed = GeneratorRunner.All().Where(generator => Changed.Contains(generator.Name)).ToList();
            Changed.Clear();
            foreach (var generator in changed)
            {
                GeneratorRunner.Run(generator);
            }
        }
    }
}
