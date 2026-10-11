using HyperRTS.Editor.Maps.Report;
using HyperRTS.Editor.Maps.Spec;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Build
{
    /// <summary>What a finished map build hands to <see cref="IMapBuildHook"/>s.</summary>
    public sealed class MapBuildResult
    {
        public MapBuildResult(MapSpec spec, MapPaths paths, MapReport report, int players, Texture2D minimap)
        {
            Spec = spec;
            Paths = paths;
            Report = report;
            Players = players;
            Minimap = minimap;
        }

        public MapSpec Spec { get; }

        public MapPaths Paths { get; }

        public MapReport Report { get; }

        public int Players { get; }

        public Texture2D Minimap { get; }
    }
}
