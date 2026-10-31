using System.Collections.Generic;
using System.Linq;
using HyperRTS.Editor.Generators;
using Unity.Pipeline.Commands;

namespace HyperRTS.Editor.Commands
{
    /// <summary>The Generate page as a pipeline command.</summary>
    public static class GeneratorCommands
    {
        [CliCommand("generate", "Run one generator by name, or every generator in order (run detached), or list them; returns each generator with what it reads and writes and its last run.", Tags = new[] { "hyperrts/generate" })]
        public static List<GeneratorEntry> Generate(
            [CliArg("name", "A generator's name as the Generate page lists it; empty runs them all")] string name = "",
            [CliArg("list", "List the generators without running any")] bool list = false)
        {
            if (list)
            {
                return Describe(GeneratorRunner.All());
            }

            if (string.IsNullOrEmpty(name))
            {
                GeneratorRunner.RunAll();
                return Describe(GeneratorRunner.All());
            }

            var generator = GeneratorRunner.Find(name);
            GeneratorRunner.Run(generator);
            return Describe(new[] { generator });
        }

        private static List<GeneratorEntry> Describe(IEnumerable<Generator> generators)
        {
            var log = GeneratorRunLog.Load();
            return generators.Select(generator => GeneratorEntry.From(generator, log.Find(generator.Name))).ToList();
        }
    }
}
