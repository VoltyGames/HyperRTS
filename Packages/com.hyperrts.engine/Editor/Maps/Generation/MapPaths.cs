using System;
using System.IO;
using System.Linq;
using UnityEditor;

namespace HyperRTS.Editor.Maps.Generation
{
    /// <summary>Where a map's generated assets live: in a Generated folder beside its spec, named after the map.</summary>
    public sealed class MapPaths
    {
        public const string SpecExtension = ".map.json";

        public const string GeneratedFolder = "Generated";

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

        /// <summary>Holds every build output, so the repo can store it apart from the hand-edited spec.</summary>
        public string Generated => $"{Folder}/{GeneratedFolder}";

        public string Scene => $"{Generated}/{Name}.unity";

        /// <summary>The SubScene <see cref="RTSSceneBuilder"/> saves next to the scene.</summary>
        public string SubScene => $"{Generated}/{Name}_Entities.unity";

        public string Terrain => $"{Generated}/{Name}_Terrain.asset";

        public string Water => $"{Generated}/{Name}_Water.asset";

        /// <summary>The terrain shader's per-map tint and wetness picture.</summary>
        public string Macro => $"{Generated}/{Name}_Macro.png";

        /// <summary>The terrain shader's per-cell crop row direction picture.</summary>
        public string RowMap => $"{Generated}/{Name}_Rows.png";

        public string TerrainMaterial => $"{Generated}/{Name}_Terrain.mat";

        /// <summary>The top-down ground colour foliage blends into at its roots.</summary>
        public string Ground => $"{Generated}/{Name}_Ground.png";

        /// <summary>What each ground cell is made of, baked for hit and wheel debris.</summary>
        public string GroundMaterials => $"{Generated}/{Name}_GroundMaterials.png";

        /// <summary>The overview view from the match camera, kept with the map for loading screens.</summary>
        public string Overview => $"{Generated}/{Name}_Overview.png";

        public string Roads => $"{Generated}/{Name}_Roads.asset";

        /// <summary>Where the row crops grow, when the map sows any.</summary>
        public string RowCrops => $"{Generated}/{Name}_RowCrops.asset";

        /// <summary>The row crop field id picture the <see cref="RowCrops"/> asset points at.</summary>
        public string RowCropIds => $"{Generated}/{Name}_RowCropIds.png";

        public string Minimap => $"{Generated}/{Name}_Minimap.png";

        public string Lighting => $"{Generated}/{Name}_Lighting.lighting";

        /// <summary>Creates the <see cref="Generated"/> folder before the first output is written.</summary>
        public void EnsureGenerated()
        {
            if (!AssetDatabase.IsValidFolder(Generated))
            {
                AssetDatabase.CreateFolder(Folder, GeneratedFolder);
            }
        }

        /// <summary>The map name of a spec file path (<c>Kalyna.map.json</c> → <c>Kalyna</c>).</summary>
        public static string MapName(string specPath) =>
            Path.GetFileName(specPath).Replace(SpecExtension, string.Empty);

        public static bool IsSpec(string path) => path.EndsWith(SpecExtension, StringComparison.Ordinal);

        public static string[] All() => AssetDatabase.FindAssets("t:TextAsset")
            .Select(AssetDatabase.GUIDToAssetPath)
            .Where(IsSpec)
            .ToArray();

        /// <summary>The spec one folder above a map scene's Generated folder, or null when the scene was not built from one.</summary>
        public static string SpecBeside(string scenePath)
        {
            if (string.IsNullOrEmpty(scenePath))
            {
                return null;
            }

            var generated = Path.GetDirectoryName(scenePath);
            if (Path.GetFileName(generated) != GeneratedFolder)
            {
                return null;
            }

            var spec = $"{Path.GetDirectoryName(generated)}/{Path.GetFileNameWithoutExtension(scenePath)}{SpecExtension}";
            return File.Exists(spec) ? spec.Replace('\\', '/') : null;
        }
    }
}
