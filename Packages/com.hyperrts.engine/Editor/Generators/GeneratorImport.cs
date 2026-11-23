using System.Collections.Generic;
using System.Linq;
using Unity.Scripting.LifecycleManagement;
using UnityEditor;

namespace HyperRTS.Editor.Generators
{
    /// <summary>
    /// Runs each <see cref="Generator"/> that has <see cref="Generator.RunOnImport"/> once after import batches that
    /// touched something it reads; of a generator with items, only the items whose inputs they touched.
    /// </summary>
    public sealed class GeneratorImport : AssetPostprocessor
    {
        [NoAutoStaticsCleanup]
        private static readonly HashSet<string> Changed = new();

        // Generator name to the names of its touched items.
        [NoAutoStaticsCleanup]
        private static readonly Dictionary<string, HashSet<string>> ChangedItems = new();

        private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            var paths = imported.Concat(deleted).Concat(moved).Concat(movedFrom).ToList();
            var pending = Changed.Count + ChangedItems.Count > 0;
            foreach (var generator in GeneratorRunner.All().Where(generator => generator.RunOnImport))
            {
                Collect(generator, paths);
            }

            // After the batch, so every changed asset has loaded; batches before the run join it.
            if (!pending && Changed.Count + ChangedItems.Count > 0)
            {
                EditorApplication.delayCall += RunChanged;
            }
        }

        private static void Collect(Generator generator, List<string> paths)
        {
            var items = generator.Items;
            if (items.Count == 0)
            {
                if (paths.Any(generator.Reads))
                {
                    Changed.Add(generator.Name);
                }

                return;
            }

            var touched = items.Where(item => paths.Any(item.Reads)).Select(item => item.Name).ToList();
            if (touched.Count == 0)
            {
                return;
            }

            if (!ChangedItems.TryGetValue(generator.Name, out var names))
            {
                names = new HashSet<string>();
                ChangedItems[generator.Name] = names;
            }

            names.UnionWith(touched);
        }

        private static void RunChanged()
        {
            var changed = new HashSet<string>(Changed);
            var changedItems = new Dictionary<string, HashSet<string>>(ChangedItems);
            Changed.Clear();
            ChangedItems.Clear();
            foreach (var generator in GeneratorRunner.All())
            {
                if (changedItems.TryGetValue(generator.Name, out var names))
                {
                    GeneratorRunner.RunItems(generator, generator.Items.Where(item => names.Contains(item.Name)).ToList());
                }
                else if (changed.Contains(generator.Name))
                {
                    GeneratorRunner.Run(generator);
                }
            }
        }
    }
}
