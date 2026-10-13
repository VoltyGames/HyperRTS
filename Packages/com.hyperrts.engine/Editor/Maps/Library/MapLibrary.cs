using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HyperRTS.Editor.Maps.Data;

namespace HyperRTS.Editor.Maps.Library
{
    /// <summary>
    /// The game's map library: one JSON file per item, named <c>&lt;Key&gt;.&lt;type&gt;.json</c> and kept anywhere
    /// under the root (layers and trees beside their art, the rest in a library folder). Keys are unique per type.
    /// Items are read when first asked for; a missing, duplicate or unreadable file throws naming the file.
    /// </summary>
    public sealed class MapLibrary
    {
        /// <summary>Every item class in listing and generation order: materials before the props whose models use them.</summary>
        private static readonly Type[] Types =
        {
            typeof(MapClimateLayer),
            typeof(MapMaterial),
            typeof(MapClimateDetail),
            typeof(MapClimateTree),
            typeof(MapRoadClass),
            typeof(MapClimateSurface),
            typeof(MapFarmlandKit),
            typeof(MapBeltKit),
            typeof(MapWoodlandKit),
            typeof(MapSettlementKit),
            typeof(MapPaletteEntry),
            typeof(MapClimate),
            typeof(MapPalette),
            typeof(MapRules),
        };

        private static readonly Dictionary<Type, string> TypeNames =
            Types.ToDictionary(type => type, type => MapLibraryItemAttribute.Require(type).Name);

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

        /// <summary>Every library item class (<c>MapClimateLayer</c>, <c>MapClimateSurface</c>...), in listing order.</summary>
        public static IReadOnlyList<Type> ItemTypes => Types;

        /// <summary>The library's type name for <typeparamref name="T"/> (<c>surface</c>, <c>prop</c>...).</summary>
        public static string TypeName<T>() => TypeName(typeof(T));

        public static string TypeName(Type type) => TypeNames.TryGetValue(type, out var name)
            ? name
            : throw new ArgumentException($"HyperRTS: {type.Name} is not a map library item class.", nameof(type));

        /// <summary>The item type and key a file at <paramref name="file"/> defines, or a null type when it is no library file.</summary>
        public static (Type Type, string Key) ParseFile(string file)
        {
            var stem = Path.GetFileNameWithoutExtension(file);
            var dot = stem.LastIndexOf('.');
            var isJson = file.EndsWith(".json", StringComparison.Ordinal);
            if (!isJson || dot < 0)
            {
                return (null, null);
            }

            var type = TypeNames.FirstOrDefault(pair => pair.Value == stem[(dot + 1)..]).Key;
            return (type, type == null ? null : stem[..dot]);
        }

        /// <summary>Every key of a type, in ordinal order.</summary>
        public IReadOnlyList<string> Keys<T>() => Keys(typeof(T));

        public IReadOnlyList<string> Keys(Type type) => _files[TypeName(type)].Keys.ToList();

        /// <summary>Whether the library holds an item of <paramref name="type"/> called <paramref name="key"/>.</summary>
        public bool Contains(Type type, string key) => _files[TypeName(type)].ContainsKey(key ?? string.Empty);

        /// <summary>The project path of an item's file.</summary>
        public string FilePath<T>(string key) => FilePath(typeof(T), key);

        public string FilePath(Type type, string key) =>
            _files[TypeName(type)].TryGetValue(key, out var file) ? file : throw Missing(type, key, null);

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
                loaded = MapLibraryItemFile.Read(typeof(T), file);
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
                var found = keys.Count == 0 ? "none" : string.Join(", ", keys.Select(FilePath<T>));
                throw new InvalidOperationException(
                    $"HyperRTS: map building needs exactly one *.{TypeName<T>()}.json under '{Root}'; found {found}.");
            }

            return keys[0];
        }

        private void Index(string file)
        {
            var (type, key) = ParseFile(file);
            if (type == null)
            {
                return;
            }

            var typeName = TypeName(type);
            if (key.Length == 0 || key.Contains('.'))
            {
                throw new InvalidOperationException(
                    $"HyperRTS: map library file '{file}' has no usable key; name it <Key>.{typeName}.json.");
            }

            var files = _files[typeName];
            if (files.ContainsKey(key))
            {
                throw new InvalidOperationException(
                    $"HyperRTS: map library files '{files[key]}' and '{file}' both define {typeName} '{key}'.");
            }

            files.Add(key, file);
        }

        private Exception Missing<T>(string key, string referrer) => Missing(typeof(T), key, referrer);

        private Exception Missing(Type type, string key, string referrer)
        {
            var by = string.IsNullOrEmpty(referrer) ? string.Empty : $"'{referrer}' names it, but ";
            return new KeyNotFoundException(
                $"HyperRTS: no map library {TypeName(type)} '{key}': {by}there is no {key}.{TypeName(type)}.json under '{Root}'.");
        }
    }
}
