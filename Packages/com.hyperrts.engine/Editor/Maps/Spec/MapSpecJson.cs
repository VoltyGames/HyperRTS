using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HyperRTS.Editor.Common.Json;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Unity.Scripting.LifecycleManagement;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Spec
{
    /// <summary>Reads and writes <c>&lt;Map&gt;.map.json</c>, laid out by <see cref="JsonFormat"/>.</summary>
    public static class MapSpecJson
    {
        [NoAutoStaticsCleanup]
        private static readonly JsonSerializerSettings ReadSettings = new()
        {
            MissingMemberHandling = MissingMemberHandling.Error,
            DateParseHandling = DateParseHandling.None,
            FloatParseHandling = FloatParseHandling.Double,
            ContractResolver = new SerializedFieldsResolver(),
        };

        /// <summary>A key the spec has no field for, or a value of the wrong shape, throws naming its path.</summary>
        public static MapSpec Read(string json)
        {
            try
            {
                return JsonConvert.DeserializeObject<MapSpec>(json, ReadSettings)
                    ?? throw new InvalidOperationException("HyperRTS: map spec must be a JSON object.");
            }
            catch (JsonException e)
            {
                throw new InvalidOperationException($"HyperRTS: map spec is not valid: {e.Message}", e);
            }
        }

        public static string Write(MapSpec spec) => JsonFormat.Format(JsonUtility.ToJson(spec, false));

        /// <summary>A deep copy of a spec or one of its features, through its JSON.</summary>
        public static T Clone<T>(T value) => JsonUtility.FromJson<T>(JsonUtility.ToJson(value));

        /// <summary>A deep copy of a feature known only by <paramref name="type"/>.</summary>
        public static object Clone(object value, Type type) => JsonUtility.FromJson(JsonUtility.ToJson(value), type);

        // Reads the public fields JsonUtility writes, not the properties that wrap them.
        private sealed class SerializedFieldsResolver : DefaultContractResolver
        {
            protected override List<MemberInfo> GetSerializableMembers(Type objectType) => objectType
                .GetFields(BindingFlags.Public | BindingFlags.Instance)
                .Where(field => !field.IsNotSerialized)
                .Cast<MemberInfo>()
                .ToList();
        }
    }
}
