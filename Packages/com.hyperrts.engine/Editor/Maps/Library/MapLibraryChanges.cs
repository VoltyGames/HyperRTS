using UnityEditor;

namespace HyperRTS.Editor.Maps.Library
{
    /// <summary>
    /// Counts project changes, so library views that hold what they read (nothing caches <see cref="MapLibrary"/>
    /// itself) reload when the number moves.
    /// </summary>
    [InitializeOnLoad]
    public static class MapLibraryChanges
    {
        static MapLibraryChanges() => EditorApplication.projectChanged += () => Version++;

        public static int Version { get; private set; }
    }
}
