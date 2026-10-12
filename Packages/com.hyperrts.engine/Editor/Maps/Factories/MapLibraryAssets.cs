using System;
using System.Collections.Generic;
using System.Linq;
using HyperRTS.Editor.Common;
using HyperRTS.Editor.Maps.Library;
using UnityEditor;

namespace HyperRTS.Editor.Maps.Factories
{
    /// <summary>
    /// Runs the <see cref="IMapAssetFactory"/> each library item names, so the item's JSON is the source of its Unity
    /// assets. Items without a <c>factory</c> point at assets made elsewhere and are skipped. Item types run in
    /// <see cref="MapLibrary.ItemTypes"/> order, so materials exist before the props whose models use them.
    /// </summary>
    public static class MapLibraryAssets
    {
        /// <summary>The assets of every item in the library; returns the paths written.</summary>
        public static IReadOnlyList<string> GenerateAll(MapLibrary library) =>
            MapLibrary.ItemTypes.SelectMany(type => GenerateAll(library, type)).ToList();

        /// <summary>The assets of every item of <paramref name="type"/>.</summary>
        public static IReadOnlyList<string> GenerateAll(MapLibrary library, Type type)
        {
            var factories = Factories();
            return library.Keys(type).SelectMany(key => Run(library, type, key, factories)).ToList();
        }

        /// <summary>The assets of one item.</summary>
        public static IReadOnlyList<string> Generate(MapLibrary library, Type type, string key) =>
            Run(library, type, key, Factories());

        /// <summary>The assets of the items in <paramref name="files"/> (library files; others are ignored), in type order.</summary>
        public static IReadOnlyList<string> GenerateFiles(MapLibrary library, IEnumerable<string> files)
        {
            var factories = Factories();
            var order = MapLibrary.ItemTypes.ToList();
            return files.Select(MapLibrary.ParseFile)
                .Where(item => item.Type != null && library.Contains(item.Type, item.Key))
                .OrderBy(item => order.IndexOf(item.Type))
                .ThenBy(item => item.Key, StringComparer.Ordinal)
                .SelectMany(item => Run(library, item.Type, item.Key, factories))
                .ToList();
        }

        /// <summary>Every factory in the project by kind; two with one kind is an error.</summary>
        public static Dictionary<string, IMapAssetFactory> Factories()
        {
            var factories = new Dictionary<string, IMapAssetFactory>(StringComparer.Ordinal);
            foreach (var factory in TypeDiscovery.Instances<IMapAssetFactory>())
            {
                if (!factories.TryAdd(factory.Kind, factory))
                {
                    throw new InvalidOperationException(
                        $"HyperRTS: map asset factories {factories[factory.Kind].GetType().FullName} and " +
                        $"{factory.GetType().FullName} both have kind '{factory.Kind}'.");
                }
            }

            return factories;
        }

        private static IReadOnlyList<string> Run(MapLibrary library, Type type, string key,
            Dictionary<string, IMapAssetFactory> factories)
        {
            var request = new MapAssetRequest(library, type, key, library.FilePath(type, key));
            var kind = request.Refs.factory;
            if (string.IsNullOrEmpty(kind))
            {
                return Array.Empty<string>();
            }

            if (!factories.TryGetValue(kind, out var factory))
            {
                throw request.Error($"names factory '{kind}', which no IMapAssetFactory has as its Kind");
            }

            var written = factory.Generate(request);
            AssetDatabase.SaveAssets();
            return written;
        }
    }
}
