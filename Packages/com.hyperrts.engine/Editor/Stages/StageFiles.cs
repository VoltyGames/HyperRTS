using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace HyperRTS.Editor.Stages;

/// <summary>What a stage reads and writes, from the project folder, which give its state by their times and its log.</summary>
/// <param name="logMarksOutputs">True for a command that rewrites one of its own inputs: a good run's log time counts as when it wrote its outputs.</param>
public sealed class StageFiles(StageLogFile log, IEnumerable<string> inputs, IEnumerable<string> outputs, bool logMarksOutputs = false)
{
    private readonly StageLogFile _log = log;
    private readonly List<string> _inputs = inputs.ToList();
    private readonly List<string> _outputs = outputs.ToList();
    private readonly bool _logMarksOutputs = logMarksOutputs;

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
        var made = _outputs.Min(File.GetLastWriteTimeUtc);
        if (_logMarksOutputs && _log.Exists && _log.WrittenUtc > made)
        {
            made = _log.WrittenUtc;
        }

        return newestInput > made ? StageState.OutOfDate : StageState.Done;
    }
}
