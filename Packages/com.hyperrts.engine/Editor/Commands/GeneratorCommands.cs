using System.Collections.Generic;
using System.Linq;
using HyperRTS.Editor.Generators;
using Unity.Pipeline.Commands;

namespace HyperRTS.Editor.Commands
{
    /// <summary>The Generate page as a pipeline command.</summary>
    public static class GeneratorCommands
    {
        [CliCommand("generate", "Run one generator by name, or every generator in order (run detached); returns the generators run.", Tags = new[] { "hyperrts/generate" })]
        public static List<string> Generate([CliArg("name", "A generator's name as the Generate page lists it; empty runs them all")] string name = "")
        {
            if (string.IsNullOrEmpty(name))
            {
                GeneratorRunner.RunAll();
                return GeneratorRunner.All().Select(generator => generator.Name).ToList();
            }

            var generator = GeneratorRunner.Find(name);
            GeneratorRunner.Run(generator);
            return new List<string> { generator.Name };
        }
    }
}
