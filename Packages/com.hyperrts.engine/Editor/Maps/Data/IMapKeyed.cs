namespace HyperRTS.Editor.Maps.Data
{
    /// <summary>A climate, palette or rules entry that specs and kits name by its key.</summary>
    public interface IMapKeyed
    {
        string Key { get; }
    }
}
