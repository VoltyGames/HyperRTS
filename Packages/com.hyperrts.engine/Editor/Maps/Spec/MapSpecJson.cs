using System;
using System.Collections.Generic;
using HyperRTS.Editor.Maps.Json;
using Newtonsoft.Json.Linq;
using Unity.Scripting.LifecycleManagement;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Spec
{
    /// <summary>Reads and writes <c>&lt;Map&gt;.map.json</c>, laid out by <see cref="MapJsonFormat"/>.</summary>
    public static class MapSpecJson
    {
        // A new entry of each feature list as its class starts, which a save leaves out of an added entry.
        [NoAutoStaticsCleanup]
        private static readonly Lazy<IReadOnlyDictionary<string, JToken>> EntryDefaults = new(FindEntryDefaults);

        /// <summary>A field the spec doesn't read throws naming its path.</summary>
        public static MapSpec Read(string json)
        {
            var spec = new MapSpec();
            MapJsonFormat.ReadInto(json, spec, "map spec", "map spec");
            return spec;
        }

        public static string Write(MapSpec spec) => MapJsonFormat.Format(JsonUtility.ToJson(spec, false));

        /// <summary>
        /// Writes <paramref name="spec"/> over the spec in <paramref name="fileJson"/> by changing only what differs (see
        /// <see cref="MapJsonChanges"/>), so a saved edit diffs like a hand edit.
        /// </summary>
        public static string WriteOver(string fileJson, MapSpec spec)
        {
            var before = MapJsonFormat.Parse(Write(Read(fileJson)));
            var after = MapJsonFormat.Parse(Write(spec));
            return MapJsonFormat.Write(MapJsonChanges.Apply(MapJsonFormat.Parse(fileJson), before, after, EntryDefaults.Value));
        }

        private static IReadOnlyDictionary<string, JToken> FindEntryDefaults()
        {
            var defaults = new Dictionary<string, JToken>();
            foreach (var field in typeof(MapSpec).GetFields())
            {
                var type = field.FieldType;
                var list = type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>);
                var entry = list ? type.GetGenericArguments()[0] : null;
                if (entry != null && entry.IsClass && entry != typeof(string))
                {
                    // Through the same layout as a written spec, so 20.0 compares equal to the file's 20.
                    defaults[field.Name] = MapJsonFormat.Parse(MapJsonFormat.Format(JsonUtility.ToJson(Activator.CreateInstance(entry))));
                }
            }

            return defaults;
        }

        /// <summary>Equal when both specs serialize to the same JSON.</summary>
        public static bool SameContent(MapSpec a, MapSpec b) => JsonUtility.ToJson(a) == JsonUtility.ToJson(b);
    }
}
