using System;
using System.IO;
using System.Linq;
using UnityEditor;

namespace HyperRTS.Editor.Maps.Generation
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
        public static string MapName(string specPath) =>
            Path.GetFileName(specPath).Replace(SpecExtension, string.Empty);

        public static bool IsSpec(string path) => path.EndsWith(SpecExtension, StringComparison.Ordinal);

        /// <summary>Every map spec in the project.</summary>
        public static string[] All() => AssetDatabase.FindAssets("t:TextAsset")
            .Select(AssetDatabase.GUIDToAssetPath)
            .Where(IsSpec)
            .ToArray();

        /// <summary>The spec saved next to a map scene, or null when the scene was not built from one.</summary>
        public static string SpecBeside(string scenePath)
        {
            if (string.IsNullOrEmpty(scenePath))
            {
                return null;
            }

            var spec = Path.ChangeExtension(scenePath, null) + SpecExtension;
            return File.Exists(spec) ? spec.Replace('\\', '/') : null;
        }
    }
}
