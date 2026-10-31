using System;
using System.IO;
using HyperRTS.Editor.Maps.Data;
using HyperRTS.Editor.Maps.Library;
using HyperRTS.Editor.Maps.Spec;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Generation
{
    /// <summary>
    /// A spec and what it builds with from the game's map library: the palette, the rules and the climate it names.
    /// Disposing it destroys those library copies.
    /// </summary>
    public sealed class MapProject : IDisposable
    {
        private readonly MapLibrary _library;

        private MapProject(MapSpec spec, string specPath, MapLibrary library)
        {
            Spec = spec;
            Paths = new MapPaths(specPath, spec.name);
            _library = library;
            Palette = library.Palette();
            Rules = library.Rules();
            Climate = library.Climate(spec.climate, specPath);
        }

        public MapSpec Spec { get; }

        public MapPaths Paths { get; }

        public MapPalette Palette { get; }

        public MapRules Rules { get; }

        public MapClimate Climate { get; }

        /// <summary>Reads the spec at <paramref name="specPath"/> and loads what it needs from the map library.</summary>
        public static MapProject Open(string specPath) =>
            Create(MapSpecJson.Read(File.ReadAllText(specPath)), specPath);

        /// <summary>
        /// A project for a spec held in memory (an editor's unsaved document) that lives at <paramref name="specPath"/>.
        /// The spec is copied first, since generation refines it in place.
        /// </summary>
        public static MapProject FromSpec(MapSpec spec, string specPath) =>
            Create(JsonUtility.FromJson<MapSpec>(JsonUtility.ToJson(spec)), specPath);

        private static MapProject Create(MapSpec spec, string specPath)
        {
            var library = MapLibrary.Load();
            try
            {
                return new MapProject(spec, specPath, library);
            }
            catch
            {
                library.Dispose();
                throw;
            }
        }

        public void Dispose() => _library.Dispose();
    }
}
