using System;
using System.Collections.Generic;
using UnityEditor;
using Object = UnityEngine.Object;

namespace HyperRTS.Editor.Maps.Library
{
    /// <summary>
    /// The project asset paths and key lists a library file names, read from the same JSON as the item itself
    /// (JsonUtility skips the keys a type doesn't have). Items resolve their asset fields through <see cref="Asset{T}"/>.
    /// </summary>
    [Serializable]
    public sealed class MapLibraryRefs
    {
        public string terrainLayer;
        public string mesh;
        public string texture;
        public string prefab;
        public string destructible;
        public string material;
        public string water;
        public string atmosphere;
        public string skybox;
        public string rig;
        public List<MapResourcePath> startingResources = new();

        public List<string> surfaces = new();
        public List<string> roadClasses = new();
        public List<string> farmlandKits = new();
        public List<string> beltKits = new();
        public List<string> woodlandKits = new();

        /// <summary>The library file these were read from (project-relative), named in every error.</summary>
        [NonSerialized]
        public string file;

        /// <summary>
        /// The asset at <paramref name="path"/>, or null when the file leaves the field empty (the map checks report
        /// a required asset that is missing). A path that doesn't load as a <typeparamref name="T"/> throws.
        /// </summary>
        public T Asset<T>(string path, string field) where T : Object
        {
            if (string.IsNullOrEmpty(path))
            {
                return null;
            }

            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            return asset != null
                ? asset
                : throw new InvalidOperationException(
                    $"HyperRTS: map library file '{file}': {field} '{path}' is not a {typeof(T).Name} asset.");
        }
    }
}
