using System;
using System.Collections.Generic;
using HyperRTS.Editor.Maps.Data;
using HyperRTS.Presentation.Environment.RowCrops;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HyperRTS.Editor.Maps.Library
{
    /// <summary>The asset paths and key lists a library file names, read from the same JSON as the item.</summary>
    [Serializable]
    public sealed class MapLibraryRefs
    {
        [MapAsset(typeof(TerrainLayer))]
        public string terrainLayer;
        [MapAsset(typeof(Mesh))]
        public List<string> meshes = new();
        [MapAsset(typeof(GameObject))]
        public string prefab;
        [MapAsset(typeof(GameObject))]
        public string backdropModel;
        [MapAsset(typeof(GameObject))]
        public List<string> prefabs = new();
        [MapAsset(typeof(GameObject))]
        public List<string> destructibles = new();
        [MapAsset(typeof(Material))]
        public string material;
        [MapAsset(typeof(Material))]
        public string water;
        [MapAsset(typeof(GameObject))]
        public string rig;
        [MapAsset(typeof(RowCrop))]
        public string crop;
        public List<MapResourcePath> startingResources = new();
        public List<MapWeatherPath> weathers = new();

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

        /// <summary>The project-relative library file, named in every error.</summary>
        [NonSerialized]
        public string file;

        /// <summary>Null for an empty path, which the map checks report. A path of the wrong type throws.</summary>
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

        /// <summary>An empty or wrong-typed path throws naming its index.</summary>
        public List<T> Assets<T>(List<string> paths, string field) where T : Object
        {
            var assets = new List<T>();
            for (var i = 0; i < paths.Count; i++)
            {
                var name = $"{field}[{i}]";
                if (string.IsNullOrEmpty(paths[i]))
                {
                    throw new InvalidOperationException($"HyperRTS: map library file '{file}': {name} is empty.");
                }

                assets.Add(Asset<T>(paths[i], name));
            }

            return assets;
        }
    }
}
