namespace HyperRTS.Editor.Maps.Common;

/// <summary>One error or warning and the spec feature it is about.</summary>
/// <param name="Message">What is wrong.</param>
/// <param name="Feature"><see cref="MapIssueFeature.None"/> when the issue is about the map as a whole.</param>
public readonly record struct MapIssue(string Message, MapIssueFeature Feature)
{
    public override string ToString() => Message;
}
