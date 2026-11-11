using HyperRTS.Simulation.Match;

namespace HyperRTS.Network.Session
{
    /// <summary>Which scene a <see cref="NetworkSession"/> start streams into its worlds, and the match setup they get.</summary>
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

        /// <summary>Written into every world of the session before the scene loads; null leaves the scene's own setup.</summary>
        public readonly MatchSetup? Setup;

        private SessionScene(LoadMode mode, string path, MatchSetup? setup)
        {
            Mode = mode;
            Path = path;
            Setup = setup;
        }

        public static SessionScene ReloadActive => new(LoadMode.ReloadActive, null, null);

        public static SessionScene Keep => new(LoadMode.Keep, null, null);

        /// <summary>A scene in the build (or, in the editor, any project path such as <c>Assets/Maps/Map.unity</c>).</summary>
        public static SessionScene Load(string path) => new(LoadMode.Load, path, null);

        public SessionScene WithSetup(MatchSetup? setup) => new(Mode, Path, setup);
    }
}
