using System;
using System.IO;
using System.Linq;
using HyperRTS.Editor.Maps.Data;
using HyperRTS.Editor.Maps.Spec;
using UnityEditor;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Build
{
    /// <summary>The game's map assets: its one palette and one rules asset, and the climate a spec names.</summary>
    public sealed class MapProject
    {
        private MapProject(MapSpec spec, MapPaths paths, MapPalette palette, MapRules rules, MapClimate climate)
        {
            Spec = spec;
            Paths = paths;
            Palette = palette;
            Rules = rules;
            Climate = climate;
        }

        public MapSpec Spec { get; }

        public MapPaths Paths { get; }

        public MapPalette Palette { get; }

        public MapRules Rules { get; }

        public MapClimate Climate { get; }

        /// <summary>Reads the spec at <paramref name="specPath"/> and loads the project assets it needs.</summary>
        public static MapProject Open(string specPath)
        {
            var spec = JsonUtility.FromJson<MapSpec>(File.ReadAllText(specPath));
            var paths = new MapPaths(specPath, spec.name);
            return new MapProject(spec, paths, Single<MapPalette>(), Single<MapRules>(), Named<MapClimate>(spec.climate));
        }

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
