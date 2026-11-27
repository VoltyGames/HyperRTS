namespace HyperRTS.Editor.Maps.Layout;

/// <summary>One placed copy of an authored feature: the authored data, the copy in map space and how it got there.</summary>
public sealed class MapCopy<T>(T authored, T placed, MapTransform transform, int index, int copy) where T : class
{
    public T Authored { get; } = authored;

    /// <summary>The feature in map space; equals <see cref="Authored"/>'s values for the identity copy.</summary>
    public T Placed { get; } = placed;

    public MapTransform Transform { get; } = transform;

    /// <summary>Position of the authored feature in its spec array; with the seed it keys random scattering.</summary>
    public int Index { get; } = index;

    public int Copy { get; } = copy;
}
