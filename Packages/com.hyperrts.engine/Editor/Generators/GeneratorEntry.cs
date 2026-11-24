using System.Collections.Generic;
using System.Linq;

namespace HyperRTS.Editor.Generators
{
    /// <summary>A generator as the <c>generator</c> commands return it, with its items.</summary>
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
        public List<GeneratorItemEntry> items;

        public static GeneratorEntry From(Generator generator, GeneratorRunLog log)
        {
            var last = log.Find(generator.Name, "");
            var items = generator.Items;
            return new GeneratorEntry
            {
                name = generator.Name,
                group = generator.Group,
                description = generator.Description,
                sources = generator.Sources,
                outputs = generator.Outputs,
                lastRun = last?.time,
                error = last?.error,
                outOfDate = generator.IsOutOfDate(items, log),
                items = items.Select(item => GeneratorItemEntry.From(generator, item, log)).ToList(),
            };
        }
    }
}
