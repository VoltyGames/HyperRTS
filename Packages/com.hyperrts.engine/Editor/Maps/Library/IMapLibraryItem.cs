namespace HyperRTS.Editor.Maps.Library
{
    /// <summary>
    /// An item a map library file holds (<c>&lt;Key&gt;.&lt;type&gt;.json</c>). The library reads its fields from the
    /// JSON, then hands it the key from the file name and the asset paths the file names, for it to resolve.
    /// </summary>
    public interface IMapLibraryItem
    {
        void Load(string key, MapLibraryRefs refs);
    }
}
