using System;
using System.IO;

namespace HyperRTS.Editor.Maps.Build
{
    /// <summary>Where a map's generated assets live: next to its spec, named after the map.</summary>
    public sealed class MapPaths
    {
        public const string SpecExtension = ".map.json";

        public MapPaths(string specPath, string name)
        {
            var file = Path.GetFileName(specPath);
            if (file != name + SpecExtension)
            {
                throw new ArgumentException($"Spec '{specPath}' must be named after its map: {name}{SpecExtension}.");
            }

            Spec = specPath.Replace('\\', '/');
            Folder = Path.GetDirectoryName(Spec)?.Replace('\\', '/');
            Name = name;
        }

        public string Spec { get; }

        public string Folder { get; }

        public string Name { get; }

        public string Scene => $"{Folder}/{Name}.unity";

        /// <summary>The SubScene <see cref="RTSSceneBuilder"/> saves next to the scene.</summary>
        public string SubScene => $"{Folder}/{Name}_Entities.unity";

        public string Terrain => $"{Folder}/{Name}_Terrain.asset";

        public string Water => $"{Folder}/{Name}_Water.asset";

        /// <summary>The terrain shader's per-map tint and wetness picture.</summary>
        public string Macro => $"{Folder}/{Name}_Macro.png";

        /// <summary>The terrain's HyperRTS/Terrain material.</summary>
        public string TerrainMaterial => $"{Folder}/{Name}_Terrain.mat";

        /// <summary>The top-down ground colour foliage blends into at its roots.</summary>
        public string Ground => $"{Folder}/{Name}_Ground.png";

        /// <summary>The overview view from the match camera, kept with the map for loading screens.</summary>
        public string Overview => $"{Folder}/{Name}_Overview.png";

        public string Roads => $"{Folder}/{Name}_Roads.asset";

        public string Minimap => $"{Folder}/{Name}_Minimap.png";

        /// <summary>The map name of a spec file path (<c>Kalyna.map.json</c> → <c>Kalyna</c>).</summary>
        public static string NameOf(string specPath) =>
            Path.GetFileName(specPath).Replace(SpecExtension, string.Empty);
    }
}
