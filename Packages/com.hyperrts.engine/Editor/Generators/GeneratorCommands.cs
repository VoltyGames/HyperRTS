using System.Collections.Generic;
using System.Linq;
using HyperRTS.Editor.Generators;
using Unity.Pipeline.Commands;

namespace HyperRTS.Editor.Generators
{
    /// <summary>The Build page's Generators tab as a pipeline command.</summary>
    public static class GeneratorCommands
    {
        [CliCommand("generator", "run", "Run one generator by name, or every generator in order, or list them; returns each generator with what it reads and writes and its last run.", Tags = new[] { "hyperrts/generate" })]
        public static List<GeneratorEntry> Generate(
            [CliArg("name", "A generator's name as the Generators tab lists it; empty runs them all")] string name = "",
            [CliArg("stale", "With no name, run only the generators that are out of date")] bool stale = false,
            [CliArg("list", "List the generators without running any")] bool list = false)
        {
            if (list)
            {
                return Describe(GeneratorRunner.All());
            }

            if (string.IsNullOrEmpty(name))
            {
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
            GeneratorRunner.Run(generator);
            return Describe(new[] { generator });
        }

        private static List<GeneratorEntry> Describe(IEnumerable<Generator> generators)
        {
            var log = GeneratorRunLog.Load();
            return generators.Select(generator => GeneratorEntry.From(generator, log)).ToList();
        }
    }
}
