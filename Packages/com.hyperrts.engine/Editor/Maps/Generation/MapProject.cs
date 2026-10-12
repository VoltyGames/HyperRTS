using System.IO;
using HyperRTS.Editor.Maps.Data;
using HyperRTS.Editor.Maps.Library;
using HyperRTS.Editor.Maps.Spec;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Generation
{
    /// <summary>A spec and what it builds with from the game's map library: the palette, the rules and the climate it names.</summary>
    public sealed class MapProject
    {
        private MapProject(MapSpec spec, MapPaths paths, MapPalette palette, MapRules rules, MapClimate climate)
        {
            Spec = spec;
            Paths = paths;
            Palette = palette;
            Rules = rules;
            Climate = climate;
        }

        public MapSpec Spec { get; }

        public MapPaths Paths { get; }

        public MapPalette Palette { get; }

        public MapRules Rules { get; }

        public MapClimate Climate { get; }

        /// <summary>Reads the spec at <paramref name="specPath"/> and loads what it needs from the map library.</summary>
        public static MapProject Open(string specPath)
        {
            var spec = JsonUtility.FromJson<MapSpec>(File.ReadAllText(specPath));
            var paths = new MapPaths(specPath, spec.name);
            var library = MapLibrary.Load();
            return new MapProject(spec, paths, library.Palette(), library.Rules(), library.Climate(spec.climate, specPath));
        }
    }
}
