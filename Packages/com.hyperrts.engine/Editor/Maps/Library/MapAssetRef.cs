using System;
using UnityEditor;
using Object = UnityEngine.Object;

namespace HyperRTS.Editor.Maps.Library
{
    /// <summary>
    /// A library file's link to a project asset: the GUID it loads by, so moves and renames keep working, and the
    /// path people read and edit. <see cref="MapLibraryRefSync"/> keeps the two in step.
    /// </summary>
    [Serializable]
    public sealed class MapAssetRef
    {
        public string path;
        public string guid;

        public bool IsEmpty => string.IsNullOrEmpty(path) && string.IsNullOrEmpty(guid);

        /// <summary>A link to the asset at <paramref name="assetPath"/>, which must exist.</summary>
        public static MapAssetRef At(string assetPath)
        {
            var link = new MapAssetRef { path = assetPath, guid = AssetDatabase.AssetPathToGUID(assetPath, AssetPathToGUIDOptions.OnlyExistingAssets) };
            return string.IsNullOrEmpty(link.guid)
                ? throw new ArgumentException($"HyperRTS: no asset at '{assetPath}' to link a map library file to.", nameof(assetPath))
                : link;
        }

        /// <summary>
        /// A path that names an asset wins, since people edit paths. Otherwise the path follows the GUID, which is how
        /// a moved asset's new path lands. True when either changed.
        /// </summary>
        public bool Sync()
        {
            var pathGuid = AssetDatabase.AssetPathToGUID(path ?? string.Empty, AssetPathToGUIDOptions.OnlyExistingAssets);
            if (!string.IsNullOrEmpty(pathGuid))
            {
                var guidChanged = pathGuid != guid;
                guid = pathGuid;
                return guidChanged;
            }

            var guidPath = string.IsNullOrEmpty(guid) ? string.Empty : AssetDatabase.GUIDToAssetPath(guid);
            if (guidPath.Length == 0 || guidPath == path)
            {
                return false;
            }

            path = guidPath;
            return true;
        }

        /// <summary>The asset by GUID, or why it can't load: no GUID, no asset with it, or the wrong type.</summary>
        public Object Load(Type type, out string problem)
        {
            problem = null;
            if (string.IsNullOrEmpty(guid))
            {
                problem = $"'{path}' has no guid. Save the file in Unity to fill it in";
                return null;
            }

            var assetPath = AssetDatabase.GUIDToAssetPath(guid);
            var asset = assetPath.Length == 0 ? null : AssetDatabase.LoadAssetAtPath(assetPath, type);
            if (asset == null)
            {
                problem = $"'{path}' (guid {guid}) is not a {type.Name} asset";
            }

            return asset;
        }
    }
}
