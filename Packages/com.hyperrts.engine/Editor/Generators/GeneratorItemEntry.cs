using System.Collections.Generic;
using HyperRTS.Editor.Stages;

namespace HyperRTS.Editor.Generators
{
    /// <summary>A generator item as the <c>generator</c> commands return it.</summary>
    public sealed class GeneratorItemEntry
    {
        public string name;
        public IReadOnlyList<string> inputs;
        public IReadOnlyList<string> outputs;
        public string projectPath;
        public string lastRun;
        public string error;
        public bool outOfDate;

        public static GeneratorItemEntry From(Generator generator, GeneratorItem item, GeneratorRunLog log)
        {
            var last = log.Find(generator.Name, item.Name);
            return new GeneratorItemEntry
            {
                name = item.Name,
                inputs = item.Inputs,
                outputs = item.Outputs,
                projectPath = item.ProjectPath,
                lastRun = last?.time,
                error = last?.error,
                outOfDate = generator.State(item, log) != StageState.Done,
            };
        }
    }
}
