using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Mathematics;

namespace HyperRTS.Editor.PlayMode.Staging;

/// <summary>
/// One timed step of a <see cref="StagingFile"/>. To add a kind, subclass it with public fields named after the
/// kind's JSON keys, check them in <see cref="Check"/>, and list the kind in <see cref="StagingEntry"/>.
/// </summary>
internal abstract class StagingStep
{
    /// <summary>Position in the file, for messages.</summary>
    public int Index { get; private set; }

    /// <summary>Seconds of game time after the match is live.</summary>
    public float At { get; private set; }

    /// <summary>The step's key in the file, such as "spawn".</summary>
    public string Kind { get; private set; }

    /// <summary>The group label this step makes, or null.</summary>
    public virtual string Defines => null;

    /// <summary>Group labels this step needs to exist already.</summary>
    public virtual IEnumerable<string> Uses => Array.Empty<string>();

    /// <summary>The file or folder this step writes, so two steps never share one; null when it writes none.</summary>
    public virtual string Output => null;

    /// <summary>Later steps are worthless without this one, so its error ends the run at once.</summary>
    public virtual bool EndsRunOnError => false;

    public abstract void Run(StagingRun run);

    /// <summary>Throws an <see cref="InvalidOperationException"/> saying what is wrong with the fields, as "needs ..." or "has ..."; the file adds the step's path.</summary>
    public virtual void Check()
    {
    }

    public StagingStep Placed(int index, float at, string kind)
    {
        Index = index;
        At = at;
        Kind = kind;
        return this;
    }

    /// <summary>The key of the one option given; none or several throws.</summary>
    public static string OneOf(params (string Key, bool Given)[] options)
    {
        var found = options.Where(option => option.Given).Select(option => $"'{option.Key}'").ToList();
        if (found.Count == 1)
        {
            return options.First(option => option.Given).Key;
        }

        var keys = string.Join(", ", options.Select(option => $"'{option.Key}'"));
        throw new InvalidOperationException(found.Count == 0 ? $"needs one of {keys}" : $"has {string.Join(", ", found)} together; give one");
    }

    /// <summary>A map point written <c>[x, z]</c>.</summary>
    public static float2 Point(float[] value, string key) =>
        value is { Length: 2 } ? new float2(value[0], value[1]) : throw new InvalidOperationException($"needs '{key}' as two numbers [x, z]");

    protected static void Need(string value, string key)
    {
        if (value == null)
        {
            throw new InvalidOperationException($"needs '{key}'");
        }
    }
}
