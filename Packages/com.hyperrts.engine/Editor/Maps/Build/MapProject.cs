using System;
using System.Linq;
using HyperRTS.Editor.Maps.Data;
using UnityEditor;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Build
{
    /// <summary>The game's map assets: its one palette and one rules asset, and the climate a spec names.</summary>
    public sealed class MapProject
    {
        private MapProject(MapPalette palette, MapRules rules, MapClimate climate)
        {
            Palette = palette;
            Rules = rules;
            Climate = climate;
        }

        public MapPalette Palette { get; }

        public MapRules Rules { get; }

        public MapClimate Climate { get; }

        public static MapProject Load(string climate) =>
            new(Single<MapPalette>(), Single<MapRules>(), Named<MapClimate>(climate));

        private static T Single<T>() where T : ScriptableObject
        {
            var paths = Find<T>();
            if (paths.Length != 1)
            {
                throw new InvalidOperationException(
                    $"Map building needs exactly one {typeof(T).Name} asset in the project; found {paths.Length}.");
            }

            return AssetDatabase.LoadAssetAtPath<T>(paths[0]);
        }

        private static T Named<T>(string name) where T : ScriptableObject
        {
            var asset = Find<T>().Select(AssetDatabase.LoadAssetAtPath<T>).FirstOrDefault(a => a.name == name);
            return asset != null
                ? asset
                : throw new InvalidOperationException($"No {typeof(T).Name} asset named '{name}' in the project.");
        }

        private static string[] Find<T>() =>
            AssetDatabase.FindAssets("t:" + typeof(T).Name).Select(AssetDatabase.GUIDToAssetPath).ToArray();
    }
}
