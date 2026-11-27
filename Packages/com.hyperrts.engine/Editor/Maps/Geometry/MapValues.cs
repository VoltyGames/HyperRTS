namespace HyperRTS.Editor.Maps.Geometry;

/// <summary>One value per sample of a <see cref="MapGrid"/>, row by row from the south-west.</summary>
public sealed class MapValues<T>(MapGrid grid)
{
    public readonly MapGrid Grid = grid;
    public readonly T[] Values = new T[grid.Count];

    public T this[int x, int z]
    {
        get => Values[Grid.Index(x, z)];
        set => Values[Grid.Index(x, z)] = value;
    }

    public T this[int index]
    {
        get => Values[index];
        set => Values[index] = value;
    }
}
