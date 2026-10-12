using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HyperRTS.Editor.Maps.Data;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Library
{
    /// <summary>
    /// The game's map library: one JSON file per item, named <c>&lt;Key&gt;.&lt;type&gt;.json</c> and kept anywhere
    /// under the root (layers and trees beside their art, the rest in a library folder). Keys are unique per type.
    /// Items are read when first asked for; a missing, duplicate or unreadable file throws naming the file.
    /// </summary>
    public sealed class MapLibrary
    {
        private static readonly Dictionary<Type, string> TypeNames = new()
        {
            { typeof(MapClimateLayer), "layer" },
            { typeof(MapClimateDetail), "plant" },
            { typeof(MapClimateTree), "tree" },
            { typeof(MapRoadClass), "road" },
            { typeof(MapClimateSurface), "surface" },
            { typeof(MapFarmlandKit), "farmland" },
            { typeof(MapBeltKit), "belt" },
            { typeof(MapWoodlandKit), "woodland" },
            { typeof(MapSettlementKit), "settlement" },
            { typeof(MapPaletteEntry), "prop" },
            { typeof(MapClimate), "climate" },
            { typeof(MapPalette), "palette" },
            { typeof(MapRules), "rules" },
        };

        private readonly Dictionary<string, SortedDictionary<string, string>> _files = new();
        private readonly Dictionary<string, (object Item, MapLibraryRefs Refs)> _loaded = new();

        private MapLibrary(string root)
        {
            Root = root;
            foreach (var type in TypeNames.Values)
            {
                _files[type] = new SortedDictionary<string, string>(StringComparer.Ordinal);
            }
        }

        public string Root { get; }

        /// <summary>Indexes every library file under <paramref name="root"/> (the project's Assets folder by default).</summary>
        public static MapLibrary Load(string root = "Assets")
        {
            var library = new MapLibrary(root);
            var files = Directory.EnumerateFiles(root, "*.json", SearchOption.AllDirectories)
                .Select(file => file.Replace('\\', '/'))
                .OrderBy(file => file, StringComparer.Ordinal);
            foreach (var file in files)
            {
                library.Index(file);
            }

            return library;
        }

        /// <summary>The library's type name for <typeparamref name="T"/> (<c>surface</c>, <c>prop</c>...).</summary>
        public static string TypeName<T>() => TypeNames[typeof(T)];

        /// <summary>Every key of a type, in ordinal order.</summary>
        public IReadOnlyList<string> Keys<T>() => _files[TypeName<T>()].Keys.ToList();

        /// <summary>The project path of an item's file.</summary>
        public string FileOf<T>(string key) =>
            _files[TypeName<T>()].TryGetValue(key, out var file) ? file : throw Missing<T>(key, null);

        /// <summary>The item <paramref name="key"/>; <paramref name="referrer"/> (a file or a spec) is named if it is missing.</summary>
        public T Get<T>(string key, string referrer) where T : class, IMapLibraryItem, new() => Get<T>(key, referrer, out _);

        public T Get<T>(string key, string referrer, out MapLibraryRefs refs) where T : class, IMapLibraryItem, new()
        {
            if (!_files[TypeName<T>()].TryGetValue(key ?? string.Empty, out var file))
            {
                throw Missing<T>(key, referrer);
            }

            if (!_loaded.TryGetValue(file, out var loaded))
            {
                loaded = (Read<T>(file), ReadRefs(file));
                ((T)loaded.Item).Load(key, loaded.Refs);
                _loaded[file] = loaded;
            }

            refs = loaded.Refs;
            return (T)loaded.Item;
        }

        /// <summary>The climate a spec names, with the layers, trees and plants its surfaces and kits use.</summary>
        public MapClimate Climate(string key, string referrer) => MapClimateAssembly.Assemble(this, key, referrer);

        /// <summary>The project's one palette, holding every prop and settlement kit in the library.</summary>
        public MapPalette Palette()
        {
            var palette = Get<MapPalette>(Single<MapPalette>(), null);
            palette.entries = Keys<MapPaletteEntry>().Select(key => Get<MapPaletteEntry>(key, null)).ToList();
            palette.settlementKits = Keys<MapSettlementKit>().Select(key => Get<MapSettlementKit>(key, null)).ToList();
            return palette;
        }

        /// <summary>The project's one set of map rules.</summary>
        public MapRules Rules() => Get<MapRules>(Single<MapRules>(), null);

        private string Single<T>()
        {
            var keys = Keys<T>();
            if (keys.Count != 1)
            {
                var found = keys.Count == 0 ? "none" : string.Join(", ", keys.Select(FileOf<T>));
                throw new InvalidOperationException(
                    $"HyperRTS: map building needs exactly one *.{TypeName<T>()}.json under '{Root}'; found {found}.");
            }

            return keys[0];
        }

        private void Index(string file)
        {
            var stem = Path.GetFileNameWithoutExtension(file);
            var dot = stem.LastIndexOf('.');
            if (dot < 0 || !_files.TryGetValue(stem[(dot + 1)..], out var files))
            {
                return;
            }

            var key = stem[..dot];
            if (key.Length == 0 || key.Contains('.'))
            {
                throw new InvalidOperationException(
                    $"HyperRTS: map library file '{file}' has no usable key; name it <Key>.{stem[(dot + 1)..]}.json.");
            }

            if (files.ContainsKey(key))
            {
                throw new InvalidOperationException(
                    $"HyperRTS: map library files '{files[key]}' and '{file}' both define {stem[(dot + 1)..]} '{key}'.");
            }

            files.Add(key, file);
        }

        private static T Read<T>(string file) where T : new()
        {
            var item = new T();
            Parse(file, item);
            return item;
        }

        private static MapLibraryRefs ReadRefs(string file)
        {
            var refs = new MapLibraryRefs { file = file };
            Parse(file, refs);
            return refs;
        }

        private static void Parse(string file, object target)
        {
            try
            {
                JsonUtility.FromJsonOverwrite(File.ReadAllText(file), target);
            }
            catch (ArgumentException e)
            {
                throw new InvalidOperationException($"HyperRTS: map library file '{file}' is not valid JSON: {e.Message}", e);
            }
        }

        private Exception Missing<T>(string key, string referrer)
        {
            var by = string.IsNullOrEmpty(referrer) ? string.Empty : $"'{referrer}' names it, but ";
            return new KeyNotFoundException(
                $"HyperRTS: no map library {TypeName<T>()} '{key}': {by}there is no {key}.{TypeName<T>()}.json under '{Root}'.");
        }
    }
}
