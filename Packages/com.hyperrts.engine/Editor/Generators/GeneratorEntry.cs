using HyperRTS.Editor.Generators;

namespace HyperRTS.Editor.Generators
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
        public bool outOfDate;

        public static GeneratorEntry From(Generator generator, GeneratorRunLog log) => new()
        {
            name = generator.Name,
            group = generator.Group,
            description = generator.Description,
            sources = generator.Sources,
            outputs = generator.Outputs,
            lastRun = log.Find(generator.Name)?.time,
            error = log.Find(generator.Name)?.error,
            outOfDate = generator.IsOutOfDate(log),
        };
    }
}
