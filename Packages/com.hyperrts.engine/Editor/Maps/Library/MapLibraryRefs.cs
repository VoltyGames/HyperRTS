using System;
using System.Collections.Generic;
using HyperRTS.Editor.Maps.Data;
using HyperRTS.Presentation.Environment.RowCrops;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HyperRTS.Editor.Maps.Library
{
    /// <summary>The asset links and key lists a library file names, read from the same JSON as the item.</summary>
    [Serializable]
    public sealed class MapLibraryRefs
    {
        [MapAsset(typeof(TerrainLayer))]
        public MapAssetRef terrainLayer = new();
        [MapAsset(typeof(Mesh))]
        public List<MapAssetRef> meshes = new();
        [MapAsset(typeof(GameObject))]
        public MapAssetRef prefab = new();
        [MapAsset(typeof(GameObject))]
        public MapAssetRef backdropModel = new();
        [MapAsset(typeof(GameObject))]
        public List<MapAssetRef> prefabs = new();
        [MapAsset(typeof(GameObject))]
        public List<MapAssetRef> destructibles = new();
        [MapAsset(typeof(Material))]
        public MapAssetRef material = new();
        [MapAsset(typeof(Material))]
        public MapAssetRef terrain = new();
        [MapAsset(typeof(Material))]
        public MapAssetRef water = new();
        [MapAsset(typeof(GameObject))]
        public MapAssetRef rig = new();
        [MapAsset(typeof(RowCrop))]
        public MapAssetRef crop = new();
        [MapAsset(typeof(TextAsset))]
        public MapAssetRef card = new();
        public List<MapResourceRef> startingResources = new();
        public List<MapWeatherRef> weathers = new();

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

        /// <summary>Null for an empty link, which the map checks report. A broken link throws.</summary>
        public T Asset<T>(MapAssetRef link, string field) where T : Object
        {
            if (link.IsEmpty)
            {
                return null;
            }

            var asset = (T)link.Load(typeof(T), out var problem);
            return asset != null
                ? asset
                : throw new InvalidOperationException($"HyperRTS: map library file '{file}': {field} {problem}.");
        }

        /// <summary>An empty or broken link throws naming its index.</summary>
        public List<T> Assets<T>(List<MapAssetRef> links, string field) where T : Object
        {
            var assets = new List<T>();
            for (var i = 0; i < links.Count; i++)
            {
                var name = $"{field}[{i}]";
                if (links[i].IsEmpty)
                {
                    throw new InvalidOperationException($"HyperRTS: map library file '{file}': {name} is empty.");
                }

                assets.Add(Asset<T>(links[i], name));
            }

            return assets;
        }
    }
}
