namespace HyperRTS.Network.Session
{
    /// <summary>Which scene a <see cref="NetworkSession"/> start streams into its new worlds.</summary>
    public readonly struct SessionScene
    {
        public enum LoadMode : byte
        {
            /// <summary>Reload the active scene, for a match started from its own map scene.</summary>
            ReloadActive = 0,

            /// <summary>Load nothing: a lobby connects first and loads the map later with <see cref="NetworkSession.LoadScene"/>.</summary>
            Keep = 1,

            /// <summary>Load <see cref="Path"/>.</summary>
            Load = 2,
        }

        public readonly LoadMode Mode;
        public readonly string Path;

        private SessionScene(LoadMode mode, string path)
        {
            Mode = mode;
            Path = path;
        }

        public static SessionScene ReloadActive => new(LoadMode.ReloadActive, null);

        public static SessionScene Keep => new(LoadMode.Keep, null);

        /// <summary>A scene in the build (or, in the editor, any project path such as <c>Assets/Maps/Map.unity</c>).</summary>
        public static SessionScene Load(string path) => new(LoadMode.Load, path);
    }
}
