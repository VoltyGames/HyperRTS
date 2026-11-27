using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace HyperRTS.Editor.Common.Json;

/// <summary>
/// Reads the public fields JsonUtility writes, not the properties that wrap them. It can also require every field
/// of a non-nullable value type, so a nullable field is the optional one.
/// </summary>
internal sealed class JsonUtilityResolver(bool requireValues) : DefaultContractResolver
{
    protected override List<MemberInfo> GetSerializableMembers(Type objectType) => objectType
        .GetFields(BindingFlags.Public | BindingFlags.Instance)
        .Where(field => !field.IsNotSerialized)
        .Cast<MemberInfo>()
        .ToList();

    protected override JsonProperty CreateProperty(MemberInfo member, MemberSerialization memberSerialization)
    {
        var property = base.CreateProperty(member, memberSerialization);
        var plainValue = property.PropertyType is { IsValueType: true } type && Nullable.GetUnderlyingType(type) == null;
        if (requireValues && plainValue)
        {
            property.Required = Required.Always;
        }

        return property;
    }
}
