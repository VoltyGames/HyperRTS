using System;
using System.Collections.Generic;
using System.Linq;

namespace HyperRTS.Editor.Generators;

/// <summary>
/// One part of a <see cref="Generator"/> with its own inputs and outputs, such as one model's prefab. It runs alone
/// and is out of date on its own.
/// </summary>
public sealed class GeneratorItem(string name, IReadOnlyList<string> inputs, IReadOnlyList<string> outputs,
    string projectPath, Action run)
{
    /// <summary>Unique within its generator; the run log keys the item's runs by it.</summary>
    public string Name { get; } = name;

    /// <summary>Asset paths of the files or folders the item reads; a folder counts every file below it.</summary>
    public IReadOnlyList<string> Inputs { get; } = inputs;

    /// <summary>Asset paths the item writes.</summary>
    public IReadOnlyList<string> Outputs { get; } = outputs;

    /// <summary>The asset or folder whose Project window entry shows the item's state.</summary>
    public string ProjectPath { get; } = projectPath;

    public Action Run { get; } = run;

    /// <summary>True when this asset path is one of the <see cref="Inputs"/> or below an input folder.</summary>
    public bool Reads(string assetPath) => ReadsPath(Inputs, assetPath);

    /// <summary>True when the path is one of the inputs or below an input folder.</summary>
    public static bool ReadsPath(IEnumerable<string> inputs, string assetPath) =>
        inputs.Any(input => assetPath == input || assetPath.StartsWith(input + "/", StringComparison.Ordinal));
}
