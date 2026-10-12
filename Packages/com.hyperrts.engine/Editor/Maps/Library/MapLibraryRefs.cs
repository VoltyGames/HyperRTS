using System;
using System.Collections.Generic;
using HyperRTS.Editor.Maps.Data;
using UnityEditor;
using UnityEngine;
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
        [MapAsset(typeof(TerrainLayer))]
        public string terrainLayer;
        [MapAsset(typeof(GameObject))]
        public string mesh;
        [MapAsset(typeof(Texture2D))]
        public string texture;
        [MapAsset(typeof(GameObject))]
        public string prefab;
        [MapAsset(typeof(GameObject))]
        public string destructible;
        [MapAsset(typeof(Material))]
        public string material;
        [MapAsset(typeof(Material))]
        public string water;
        [MapAsset(typeof(GameObject))]
        public string atmosphere;
        [MapAsset(typeof(Material))]
        public string skybox;
        [MapAsset(typeof(GameObject))]
        public string rig;
        public List<MapResourcePath> startingResources = new();

        [MapKey(typeof(MapClimateSurface))]
        public List<string> surfaces = new();
        [MapKey(typeof(MapRoadClass))]
        public List<string> roadClasses = new();
        [MapKey(typeof(MapFarmlandKit))]
        public List<string> farmlandKits = new();
        [MapKey(typeof(MapBeltKit))]
        public List<string> beltKits = new();
        [MapKey(typeof(MapWoodlandKit))]
        public List<string> woodlandKits = new();

        /// <summary>
        /// The <see cref="Factories.IMapAssetFactory"/> that makes this item's assets (its <c>Kind</c>), or empty
        /// when the item only points at assets made elsewhere.
        /// </summary>
        public string factory;

        /// <summary>The raw <c>params</c> object the factory reads, kept as written so saves leave it untouched.</summary>
        [NonSerialized]
        public string factoryParams;

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
