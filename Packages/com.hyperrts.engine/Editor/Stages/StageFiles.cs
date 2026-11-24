using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace HyperRTS.Editor.Stages
{
    /// <summary>What a stage reads and writes, from the project folder, which give its state by their times and its log.</summary>
    public sealed class StageFiles
    {
        private readonly StageLogFile _log;
        private readonly List<string> _inputs;
        private readonly List<string> _outputs;

        public StageFiles(StageLogFile log, IEnumerable<string> inputs, IEnumerable<string> outputs)
        {
            _log = log;
            _inputs = inputs.ToList();
            _outputs = outputs.ToList();
        }

        /// <summary>Failed by the log, Missing without every output, Out of date when an input is newer than the oldest output.</summary>
        public StageState State()
        {
            if (_log.Failed)
            {
                return StageState.Failed;
            }

            if (_outputs.Count == 0 || _outputs.Any(path => !File.Exists(path)))
            {
                return StageState.Missing;
            }

            var inputs = _inputs.Where(File.Exists).ToList();
            var newestInput = inputs.Count == 0 ? DateTime.MinValue : inputs.Max(File.GetLastWriteTimeUtc);
            return newestInput > _outputs.Min(File.GetLastWriteTimeUtc) ? StageState.OutOfDate : StageState.Done;
        }
    }
}
