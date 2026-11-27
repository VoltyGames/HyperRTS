using System.Collections.Generic;

namespace HyperRTS.Editor.PlayMode.Staging;

/// <summary>The JSON of a staging file as written; <see cref="StagingFile"/> checks it and builds the steps.</summary>
internal sealed class StagingSpec
{
    public string scene;
    public float[] resolution;
    public List<StagingEntry> steps;
}
