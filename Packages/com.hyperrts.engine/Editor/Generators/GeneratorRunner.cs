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
        public static void RunAll() => RunInOrder(All(), "generators", Run);

        /// <summary>Runs, in order, the generators that are out of date, and of a generator with items only those items.</summary>
        public static void RunOutOfDate()
        {
            var log = GeneratorRunLog.Load();
            var outOfDate = All().Where(generator => generator.IsOutOfDate(log)).ToList();
            RunInOrder(outOfDate, "out of date generators", generator => RunOutOfDate(generator, log));
        }

        /// <summary>Runs the generator's out of date items, or the whole generator when it has none and is out of date.</summary>
        public static void RunOutOfDate(Generator generator, GeneratorRunLog log)
        {
            if (generator.Items.Count > 0)
            {
                RunItems(generator, generator.OutOfDateItems(log));
                return;
            }

            if (generator.IsOutOfDate(log))
            {
                Run(generator);
            }
        }

        private static void RunInOrder(IReadOnlyList<Generator> generators, string what, Action<Generator> run)
        {
            try
            {
                for (var i = 0; i < generators.Count; i++)
                {
                    EditorUtility.DisplayProgressBar("HyperRTS", generators[i].Name, (float)i / generators.Count);
                    run(generators[i]);
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            Debug.Log($"HyperRTS: ran {generators.Count} {what}.");
        }

        /// <summary>Runs one generator, or every item of one with items, and records how each ended in the <see cref="GeneratorRunLog"/>.</summary>
        public static void Run(Generator generator)
        {
            var items = generator.Items;
            if (items.Count > 0)
            {
                RunItems(generator, items);
                return;
            }

            try
            {
                generator.Run();
                AssetDatabase.SaveAssets();
            }
            catch (Exception exception)
            {
                GeneratorRunLog.Record(generator.Name, "", exception.Message);
                throw;
            }

            GeneratorRunLog.Record(generator.Name, "", null);
        }

        /// <summary>Runs one item and records how it ended.</summary>
        public static void Run(Generator generator, GeneratorItem item)
        {
            try
            {
                item.Run();
                AssetDatabase.SaveAssets();
            }
            catch (Exception exception)
            {
                GeneratorRunLog.Record(generator.Name, item.Name, exception.Message);
                throw;
            }

            GeneratorRunLog.Record(generator.Name, item.Name, null);
        }

        /// <summary>Runs every item even when one fails, so one broken input holds back no other, then throws naming the failed ones.</summary>
        public static void RunItems(Generator generator, IReadOnlyList<GeneratorItem> items)
        {
            var failed = new List<string>();
            foreach (var item in items)
            {
                try
                {
                    Run(generator, item);
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                    failed.Add(item.Name);
                }
            }

            if (failed.Count > 0)
            {
                throw new InvalidOperationException($"HyperRTS: {generator.Name} failed on {failed.Count} of {items.Count} items: {string.Join(", ", failed)}.");
            }
        }
    }
}
