using System;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Unity.Scripting.LifecycleManagement;

namespace HyperRTS.Editor.Common.Json;

/// <summary>Reads hand-written JSON into JsonUtility-style classes, throwing on an unknown key or a value of the wrong shape.</summary>
public static class StrictJson
{
    [NoAutoStaticsCleanup]
    private static readonly JsonSerializerOptions Optional = Options(false);

    [NoAutoStaticsCleanup]
    private static readonly JsonSerializerOptions Complete = Options(true);

    /// <summary>
    /// Throws naming <paramref name="what"/> and the bad key's path. With <paramref name="requireValues"/>, every
    /// non-nullable value-type field must be given, so a nullable field is the optional one.
    /// </summary>
    public static T Read<T>(string json, string what, bool requireValues)
    {
        try
        {
            return JsonSerializer.Deserialize<T>(json, requireValues ? Complete : Optional)
                ?? throw new InvalidOperationException($"HyperRTS: {what} must be a JSON object.");
        }
        catch (JsonException e)
        {
            var at = e.Path == null ? string.Empty : $" at {e.Path}";
            throw new InvalidOperationException($"HyperRTS: {what} is not valid{at}: {e.Message}", e);
        }
    }

    private static JsonSerializerOptions Options(bool requireValues) => new()
    {
        IncludeFields = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
        Converters = { new JsonStringEnumConverter() },
        TypeInfoResolver = new FieldResolver(requireValues),
    };

    // Builds objects from the public fields JsonUtility writes, so a property wrapping a field (warAge, WarAge) never
    // enters, not even to collide.
    private sealed class FieldResolver(bool requireValues) : DefaultJsonTypeInfoResolver
    {
        public override JsonTypeInfo GetTypeInfo(Type type, JsonSerializerOptions options)
        {
            var info = JsonTypeInfo.CreateJsonTypeInfo(type, options);
            if (info.Kind != JsonTypeInfoKind.Object)
            {
                return base.GetTypeInfo(type, options);
            }

            info.CreateObject = () => Activator.CreateInstance(type);
            foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Instance).Where(field => !field.IsNotSerialized))
            {
                var name = field.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name ?? field.Name;
                var property = info.CreateJsonPropertyInfo(field.FieldType, name);
                property.Get = field.GetValue;
                property.Set = field.SetValue;
                property.IsRequired = requireValues && field.FieldType.IsValueType && Nullable.GetUnderlyingType(field.FieldType) == null;
                info.Properties.Add(property);
            }

            return info;
        }
    }
}
