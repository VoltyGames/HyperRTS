namespace HyperRTS.Editor.Hub;

/// <summary>The count pill on a page's sidebar entry: red when any of the count is an error, amber otherwise.</summary>
public readonly record struct PageBadge(int Count, bool HasErrors)
{
    public static readonly PageBadge None = default;

    public bool IsNone => Count == 0;
}
