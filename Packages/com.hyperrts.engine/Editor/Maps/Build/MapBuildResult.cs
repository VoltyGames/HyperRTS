using HyperRTS.Editor.Maps.Climate;
using HyperRTS.Editor.Maps.Report;
using HyperRTS.Editor.Maps.Spec;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Build
{
    /// <summary>What a finished map build hands to <see cref="IMapBuildHook"/>s.</summary>
    public sealed class MapBuildResult
    {
        public MapSpec Spec { get; set; }

        public MapPaths Paths { get; set; }

        /// <summary>The climate the build used, so hooks need not reload the library.</summary>
        public MapClimate Climate { get; set; }

        public MapReport Report { get; set; }

        public int Players { get; set; }

        public Texture2D Minimap { get; set; }

        /// <summary>The map seen whole from the match camera (loading screens, menus); the last one saved in a build without graphics.</summary>
        public Texture2D Overview { get; set; }
    }
}
