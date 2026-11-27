using System;
using HyperRTS.Editor.Common.Json;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Spec;

/// <summary>Reads and writes <c>&lt;Map&gt;.map.json</c>, laid out by <see cref="JsonFormat"/>.</summary>
public static class MapSpecJson
{
    /// <summary>A key the spec has no field for, or a value of the wrong shape, throws naming its path.</summary>
    public static MapSpec Read(string json) => StrictJson.Read<MapSpec>(json, "map spec", false);

    public static string Write(MapSpec spec) => JsonFormat.Format(JsonUtility.ToJson(spec, false));

    /// <summary>A deep copy of a spec or one of its features, through its JSON.</summary>
    public static T Clone<T>(T value) => JsonUtility.FromJson<T>(JsonUtility.ToJson(value));

    /// <summary>A deep copy of a feature known only by <paramref name="type"/>.</summary>
    public static object Clone(object value, Type type) => JsonUtility.FromJson(JsonUtility.ToJson(value), type);
}
