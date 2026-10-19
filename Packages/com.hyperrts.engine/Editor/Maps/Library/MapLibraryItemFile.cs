using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HyperRTS.Editor.Maps.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Library
{
    /// <summary>
    /// Reads and writes one library file as editors see it: the item and the <see cref="MapLibraryRefs"/> fields
    /// its type reads. Written in the <see cref="MapJsonFormat"/> layout specs use, the refs first; the key is the
    /// file name, never a field.
    /// </summary>
    public static class MapLibraryItemFile
    {
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
            var fields = Fields(item);
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

            RejectUnknown(type, root, fields, file);
            return (item, refs);
        }

        /// <summary>A field neither the item nor its refs read is a typo or an old format, so it fails rather than vanishing.</summary>
        private static void RejectUnknown(Type type, JToken root, HashSet<string> fields, string file)
        {
            if (root is not JObject properties)
            {
                return;
            }

            var attribute = MapLibraryItemAttribute.Require(type);
            fields.UnionWith(attribute.Refs);
            foreach (var property in properties.Properties())
            {
                if (!fields.Contains(property.Name))
                {
                    throw new InvalidOperationException(
                        $"HyperRTS: map library file '{file}': '{property.Name}' is not a {attribute.Name} field.");
                }
            }
        }

        private static HashSet<string> Fields(object item) =>
            ((JObject)MapJsonFormat.Parse(JsonUtility.ToJson(item))).Properties().Select(p => p.Name).ToHashSet();

        public static void Write(string file, Type type, object item, MapLibraryRefs refs) =>
            File.WriteAllText(file, ToJson(type, item, refs));

        public static string ToJson(Type type, object item, MapLibraryRefs refs)
        {
            var refsJson = (JObject)MapJsonFormat.Parse(JsonUtility.ToJson(refs));
            var root = new JObject();
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
