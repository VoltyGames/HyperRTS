using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Pipeline.Commands;

namespace HyperRTS.Editor.Generators
{
    /// <summary>The Build page's Generators tab as a pipeline command.</summary>
    public static class GeneratorCommands
    {
        private const string Tag = "hyperrts/generate";

        [CliCommand("generator", "run", "Run one generator or item by name, or every generator in order; returns each generator and its items with what they read and write and their last run.", Tags = new[] { Tag })]
        public static List<GeneratorEntry> Run(
            [CliArg("name", "A generator's name as the Generators tab lists it; empty runs them all")] string name = "",
            [CliArg("item", "With a name, run only this item of the generator")] string item = "",
            [CliArg("stale", "Run only what is out of date: the generators and items, or with a name that generator's")] bool stale = false)
        {
            if (string.IsNullOrEmpty(name))
            {
                if (!string.IsNullOrEmpty(item))
                {
                    throw new ArgumentException("HyperRTS: an item needs the name of its generator.");
                }

                if (stale)
                {
                    GeneratorRunner.RunOutOfDate();
                }
                else
                {
                    GeneratorRunner.RunAll();
                }

                return Describe(GeneratorRunner.All());
            }

            var generator = GeneratorRunner.Find(name);
            if (!string.IsNullOrEmpty(item))
            {
                GeneratorRunner.Run(generator, generator.FindItem(item));
            }
            else if (stale)
            {
                GeneratorRunner.RunOutOfDate(generator, GeneratorRunLog.Load());
            }
            else
            {
                GeneratorRunner.Run(generator);
            }

            return Describe(new[] { generator });
        }

        [CliCommand("generator", "list", "List every generator and its items with what they read and write and their last run, running none.", Tags = new[] { Tag })]
        public static List<GeneratorEntry> List() => Describe(GeneratorRunner.All());

        private static List<GeneratorEntry> Describe(IEnumerable<Generator> generators)
        {
            var log = GeneratorRunLog.Load();
            return generators.Select(generator => GeneratorEntry.From(generator, log)).ToList();
        }
    }
}
