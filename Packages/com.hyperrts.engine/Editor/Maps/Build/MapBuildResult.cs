using HyperRTS.Editor.Maps.Data;
using HyperRTS.Editor.Maps.Generation;
using HyperRTS.Editor.Maps.Report;
using HyperRTS.Editor.Maps.Spec;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Build
{
    /// <summary>What a finished map build hands to <see cref="IMapBuildHook"/>s.</summary>
    public sealed class MapBuildResult
    {
        public MapBuildResult(MapSpec spec, MapPaths paths, MapClimate climate, MapReport report, int players, Texture2D minimap,
            Texture2D overview)
        {
            Spec = spec;
            Paths = paths;
            Climate = climate;
            Report = report;
            Players = players;
            Minimap = minimap;
            Overview = overview;
        }

        public MapSpec Spec { get; }

        public MapPaths Paths { get; }

        /// <summary>The climate the build used, so hooks need not reload the library.</summary>
        public MapClimate Climate { get; }

        public MapReport Report { get; }

        public int Players { get; }

        public Texture2D Minimap { get; }

        /// <summary>The map seen whole from the match camera (loading screens, menus); the last one saved in a build without graphics.</summary>
        public Texture2D Overview { get; }
    }
}
