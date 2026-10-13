using System;
using System.IO;
using HyperRTS.Editor.Maps.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Library
{
    /// <summary>
    /// Reads and writes one library file as editors see it: the item and the <see cref="MapLibraryRefs"/> fields
    /// its type reads. Written in the <see cref="MapJsonFormat"/> layout specs use, the refs first, the factory and
    /// its params object last; the key is the file name, never a field.
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
            try
            {
                var root = MapJsonFormat.Parse(json);
                JsonUtility.FromJsonOverwrite(json, item);
                JsonUtility.FromJsonOverwrite(json, refs);
                refs.factoryParams = root["params"];
            }
            catch (Exception e) when (e is ArgumentException or FormatException or InvalidOperationException)
            {
                throw new InvalidOperationException($"HyperRTS: map library file '{file}' is not valid JSON: {e.Message}", e);
            }

            return (item, refs);
        }

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

            AddFactory(root, refs);
            return MapJsonFormat.Write(root);
        }

        /// <summary>The factory name and its params object, last, when the item has a factory.</summary>
        private static void AddFactory(JObject root, MapLibraryRefs refs)
        {
            if (string.IsNullOrEmpty(refs.factory))
            {
                return;
            }

            if (refs.factoryParams == null)
            {
                throw new InvalidOperationException(
                    $"HyperRTS: map library file '{refs.file}' names factory '{refs.factory}' but has no \"params\" object.");
            }

            root.Add("factory", refs.factory);
            root.Add("params", refs.factoryParams.DeepClone());
        }
    }
}
