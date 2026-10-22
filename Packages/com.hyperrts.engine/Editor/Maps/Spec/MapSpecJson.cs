using System;
using HyperRTS.Editor.Maps.Json;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Spec
{
    /// <summary>Reads and writes <c>&lt;Map&gt;.map.json</c>, laid out by <see cref="MapJsonFormat"/>.</summary>
    public static class MapSpecJson
    {
        /// <summary>A field the spec doesn't read throws naming its path.</summary>
        public static MapSpec Read(string json)
        {
            var spec = JsonUtility.FromJson<MapSpec>(json);
            if (spec == null)
            {
                throw new FormatException("Map spec JSON is empty.");
            }

            var known = MapJsonFormat.Parse(JsonUtility.ToJson(spec));
            MapJsonFormat.RejectUnknown(MapJsonFormat.Parse(json), known, "map spec", "map spec");
            return spec;
        }

        public static string Write(MapSpec spec) => MapJsonFormat.Format(JsonUtility.ToJson(spec, false));

        /// <summary>Equal when both specs serialize to the same JSON.</summary>
        public static bool SameContent(MapSpec a, MapSpec b) => JsonUtility.ToJson(a) == JsonUtility.ToJson(b);
    }
}
