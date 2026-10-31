using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HyperRTS.Editor.Maps.Climate;
using HyperRTS.Editor.Maps.Palette;
using HyperRTS.Editor.Maps.Rules;
using Unity.Scripting.LifecycleManagement;
using UnityEditor;
using Object = UnityEngine.Object;

namespace HyperRTS.Editor.Maps.Library
{
    /// <summary>
    /// The map library: one <c>&lt;Key&gt;.&lt;type&gt;.asset</c> per item anywhere in the project. Each item loads on
    /// first use as a copy owned by this library, so a build may fill in its derived fields; disposing the library
    /// destroys the copies.
    /// </summary>
    public sealed class MapLibrary : IDisposable
    {
        /// <summary>Item types in listing order.</summary>
        [NoAutoStaticsCleanup]
        private static readonly Type[] Types =
        {
            typeof(MapClimateLayer),
            typeof(MapClimatePlant),
            typeof(MapClimateRowCrop),
            typeof(MapClimateTree),
            typeof(MapClimateDecal),
            typeof(MapRoadClass),
            typeof(MapClimateSurface),
            typeof(MapFarmlandKit),
            typeof(MapBeltKit),
            typeof(MapWoodlandKit),
            typeof(MapWarKit),
            typeof(MapSettlementKit),
            typeof(MapProp),
            typeof(MapClimate),
            typeof(MapPalette),
            typeof(MapRules),
        };

        [NoAutoStaticsCleanup]
        private static readonly Dictionary<Type, string> TypeNames =
            Types.ToDictionary(type => type, type => MapLibraryItemAttribute.Require(type).Name);

        private readonly Dictionary<Type, SortedDictionary<string, Entry>> _entries = new();

        private MapLibrary()
        {
            foreach (var type in Types)
            {
                _entries[type] = new SortedDictionary<string, Entry>(StringComparer.Ordinal);
            }
        }

        /// <summary>Indexes every library item asset in the project.</summary>
        public static MapLibrary Load()
        {
            var library = new MapLibrary();
            foreach (var type in Types)
            {
                var paths = AssetDatabase.FindAssets("t:" + type.Name)
                    .Select(AssetDatabase.GUIDToAssetPath)
                    .Where(path => AssetDatabase.GetMainAssetTypeAtPath(path) == type)
                    .Distinct()
                    .OrderBy(path => path, StringComparer.Ordinal);
                foreach (var path in paths)
                {
                    library.Index(type, path);
                }
            }

            return library;
        }

        /// <summary>A library of items made in memory, keyed by their names, for tests.</summary>
        public static MapLibrary From(params MapLibraryItem[] items)
        {
            var library = new MapLibrary();
            foreach (var item in items)
            {
                library.Add(item.GetType(), MapLibraryItem.ParseKey(item.name), new Entry(null, item));
            }

            return library;
        }

        /// <summary>Every library item class, in listing order.</summary>
        public static IReadOnlyList<Type> ItemTypes => Types;

        /// <summary>The library's type name for <typeparamref name="T"/> (<c>surface</c>, <c>prop</c>...).</summary>
        public static string TypeName<T>() => TypeName(typeof(T));

        public static string TypeName(Type type) => TypeNames.TryGetValue(type, out var name)
            ? name
            : throw new ArgumentException($"HyperRTS: {type.Name} is not a map library item class.", nameof(type));

        /// <summary>The item type and key an asset at <paramref name="path"/> is named as, or a null type when it is no library item.</summary>
        public static (Type Type, string Key) ParseFile(string path)
        {
            var stem = Path.GetFileNameWithoutExtension(path);
            var dot = stem.LastIndexOf('.');
            if (!path.EndsWith(".asset", StringComparison.Ordinal) || dot < 0)
            {
                return (null, null);
            }

            var type = TypeNames.FirstOrDefault(pair => pair.Value == stem[(dot + 1)..]).Key;
            return (type, type == null ? null : stem[..dot]);
        }

        public IReadOnlyList<string> Keys<T>() => Keys(typeof(T));

        public IReadOnlyList<string> Keys(Type type) => _entries[type].Keys.ToList();

        public bool Contains(Type type, string key) => _entries[type].ContainsKey(key ?? string.Empty);

        public string FilePath<T>(string key) => FilePath(typeof(T), key);

        /// <summary>The item's asset path, or its key for an item made in memory.</summary>
        public string FilePath(Type type, string key) =>
            _entries[type].TryGetValue(key, out var entry) ? entry.Path ?? key : throw Missing(type, key, null);

        /// <summary>A missing item names <paramref name="referrer"/>, an asset or a spec.</summary>
        public T Get<T>(string key, string referrer) where T : MapLibraryItem => (T)Get(typeof(T), key, referrer);

        public MapLibraryItem Get(Type type, string key, string referrer)
        {
            if (!_entries[type].TryGetValue(key ?? string.Empty, out var entry))
            {
                throw Missing(type, key, referrer);
            }

            entry.Copy ??= Copy(entry, key);
            return entry.Copy;
        }

        /// <summary>The climate a spec names, with the layers, trees and plants its surfaces and kits use.</summary>
        public MapClimate Climate(string key, string referrer) => MapClimateAssembly.Assemble(this, key, referrer);

        /// <summary>The project's one palette, holding every prop and settlement kit in the library.</summary>
        public MapPalette Palette()
        {
            var palette = Get<MapPalette>(Single<MapPalette>(), null);
            palette.entries = Keys<MapProp>().Select(key => Get<MapProp>(key, null)).ToList();
            palette.settlementKits = Keys<MapSettlementKit>().Select(key => Get<MapSettlementKit>(key, null)).ToList();
            return palette;
        }

        public MapRules Rules() => Get<MapRules>(Single<MapRules>(), null);

        /// <summary>The item itself rather than a copy: the asset, or the item made in memory. Edit it only to change the library.</summary>
        public MapLibraryItem Original(Type type, string key) =>
            _entries[type].TryGetValue(key, out var entry) ? Original(entry) : throw Missing(type, key, null);

        /// <summary>Destroys the copies handed out, which nothing else frees until Unity next unloads assets.</summary>
        public void Dispose()
        {
            foreach (var entry in _entries.Values.SelectMany(entries => entries.Values).Where(entry => entry.Copy != null))
            {
                Object.DestroyImmediate(entry.Copy);
                entry.Copy = null;
            }
        }

        private static MapLibraryItem Original(Entry entry) => entry.Item ?? AssetDatabase.LoadAssetAtPath<MapLibraryItem>(entry.Path);

        private static MapLibraryItem Copy(Entry entry, string key)
        {
            var original = Original(entry);
            var copy = Object.Instantiate(original);
            copy.name = original.name;
            copy.key = key;
            copy.file = entry.Path;
            copy.Load();
            return copy;
        }

        private string Single<T>()
        {
            var keys = Keys<T>();
            if (keys.Count != 1)
            {
                var found = keys.Count == 0 ? "none" : string.Join(", ", keys.Select(FilePath<T>));
                throw new InvalidOperationException(
                    $"HyperRTS: map building needs exactly one *.{TypeName<T>()}.asset; found {found}.");
            }

            return keys[0];
        }

        private void Index(Type type, string path)
        {
            var (named, key) = ParseFile(path);
            if (named != type || key.Length == 0)
            {
                throw new InvalidOperationException(
                    $"HyperRTS: map library asset '{path}' is a {TypeName(type)}; name it <Key>.{TypeName(type)}.asset.");
            }

            Add(type, key, new Entry(path, null));
        }

        private void Add(Type type, string key, Entry entry)
        {
            var entries = _entries[type];
            if (entries.TryGetValue(key, out var existing))
            {
                throw new InvalidOperationException(
                    $"HyperRTS: map library assets '{existing.Path ?? key}' and '{entry.Path ?? key}' are both {TypeName(type)} '{key}'.");
            }

            entries.Add(key, entry);
        }

        private Exception Missing(Type type, string key, string referrer)
        {
            var by = string.IsNullOrEmpty(referrer) ? string.Empty : $"'{referrer}' names it, but ";
            return new KeyNotFoundException(
                $"HyperRTS: no map library {TypeName(type)} '{key}': {by}there is no {key}.{TypeName(type)}.asset.");
        }

        /// <summary>An indexed item: its asset path (or the item itself when made in memory) and the copy once loaded.</summary>
        private sealed class Entry
        {
            public Entry(string path, MapLibraryItem item)
            {
                Path = path;
                Item = item;
            }

            public string Path { get; }

            public MapLibraryItem Item { get; }

            public MapLibraryItem Copy { get; set; }
        }
    }
}
