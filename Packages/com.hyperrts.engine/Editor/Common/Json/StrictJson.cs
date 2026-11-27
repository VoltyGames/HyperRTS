using System;
using Newtonsoft.Json;
using Unity.Scripting.LifecycleManagement;

namespace HyperRTS.Editor.Common.Json;

/// <summary>Reads hand-written JSON into JsonUtility-style classes, throwing on an unknown key or a value of the wrong shape.</summary>
public static class StrictJson
{
    [NoAutoStaticsCleanup]
    private static readonly JsonSerializerSettings Optional = Settings(false);

    [NoAutoStaticsCleanup]
    private static readonly JsonSerializerSettings Complete = Settings(true);

    /// <summary>
    /// Throws naming <paramref name="what"/> and the bad key's path. With <paramref name="requireValues"/>, every
    /// non-nullable value-type field must be given (<see cref="JsonUtilityResolver"/>).
    /// </summary>
    public static T Read<T>(string json, string what, bool requireValues)
    {
        try
        {
            return JsonConvert.DeserializeObject<T>(json, requireValues ? Complete : Optional)
                ?? throw new InvalidOperationException($"HyperRTS: {what} must be a JSON object.");
        }
        catch (JsonException e)
        {
            throw new InvalidOperationException($"HyperRTS: {what} is not valid: {e.Message}", e);
        }
    }

    private static JsonSerializerSettings Settings(bool requireValues) => new()
    {
        MissingMemberHandling = MissingMemberHandling.Error,
        DateParseHandling = DateParseHandling.None,
        FloatParseHandling = FloatParseHandling.Double,
        ContractResolver = new JsonUtilityResolver(requireValues),
    };
}
