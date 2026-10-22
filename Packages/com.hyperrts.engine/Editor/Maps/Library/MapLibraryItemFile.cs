using System;
using System.IO;
using HyperRTS.Editor.Maps.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Library
{
    /// <summary>
    /// Reads and writes one library file as editors see it: the item and the <see cref="MapLibraryRefs"/> fields
    /// its type reads. Written in the <see cref="MapJsonFormat"/> layout specs use, the version and refs first; the
    /// key is the file name, never a field.
    /// </summary>
    public static class MapLibraryItemFile
    {
        /// <summary>The format every file declares. It stays 1 until the first release: format changes migrate files in place.</summary>
        public const int Version = 1;

        /// <summary>A new item of <paramref name="type"/> with its class defaults.</summary>
        public static (object Item, MapLibraryRefs Refs) Create(Type type) =>
            (Activator.CreateInstance(type), new MapLibraryRefs());

        public static (object Item, MapLibraryRefs Refs) Read(Type type, string file) =>
            Parse(type, File.ReadAllText(file), file);

        public static (object Item, MapLibraryRefs Refs) Parse(Type type, string json, string file)
        {
            var (item, refs) = Create(type);
            refs.file = file;
            JToken root;
            try
            {
                root = MapJsonFormat.Parse(json);
                JsonUtility.FromJsonOverwrite(json, item);
                JsonUtility.FromJsonOverwrite(json, refs);
            }
            catch (Exception e) when (e is ArgumentException or FormatException or InvalidOperationException)
            {
                throw new InvalidOperationException($"HyperRTS: map library file '{file}' is not valid JSON: {e.Message}", e);
            }

            RequireVersion(root, file);
            var name = MapLibraryItemAttribute.Require(type).Name;
            MapJsonFormat.RejectUnknown(root, MapJsonFormat.Parse(ToJson(type, item, refs)), name, $"map library file '{file}'");
            return (item, refs);
        }

        private static void RequireVersion(JToken root, string file)
        {
            var version = (root as JObject)?["version"];
            if (version is not { Type: JTokenType.Integer } || version.Value<int>() != Version)
            {
                throw new InvalidOperationException(
                    $"HyperRTS: map library file '{file}' has version {version?.ToString() ?? "none"}, but the library reads version {Version}.");
            }
        }

        public static void Write(string file, Type type, object item, MapLibraryRefs refs) =>
            File.WriteAllText(file, ToJson(type, item, refs));

        public static string ToJson(Type type, object item, MapLibraryRefs refs)
        {
            var refsJson = (JObject)MapJsonFormat.Parse(JsonUtility.ToJson(refs));
            var root = new JObject { ["version"] = Version };
            foreach (var name in MapLibraryItemAttribute.Require(type).Refs)
            {
                root.Add(name, refsJson[name]);
            }

            foreach (var property in ((JObject)MapJsonFormat.Parse(JsonUtility.ToJson(item))).Properties())
            {
                root.Add(property.Name, property.Value);
            }

            return MapJsonFormat.Write(root);
        }
    }
}
