using HyperRTS.Editor.Generators;

namespace HyperRTS.Editor.Commands
{
    /// <summary>A generator as the <c>generate</c> command returns it.</summary>
    public sealed class GeneratorEntry
    {
        public string name;
        public string group;
        public string description;
        public string sources;
        public string outputs;
        public string lastRun;
        public string error;

        public static GeneratorEntry From(Generator generator, GeneratorRun last) => new()
        {
            name = generator.Name,
            group = generator.Group,
            description = generator.Description,
            sources = generator.Sources,
            outputs = generator.Outputs,
            lastRun = last?.time,
            error = last?.error,
        };
    }
}
