using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HyperRTS.Editor.Maps.Spec;
using Unity.Scripting.LifecycleManagement;

namespace HyperRTS.Editor.Maps.Designer.Features
{
    /// <summary>Every feature list of a map spec, in drawing order (areas under lines under points).</summary>
    public static class MapFeatureKinds
    {
        [NoAutoStaticsCleanup]
        public static readonly IReadOnlyList<MapFeatureKind> All = typeof(MapSpec)
            .GetFields(BindingFlags.Public | BindingFlags.Instance)
            .Where(IsFeatureList)
            .Select(field => new MapFeatureKind(field))
            .OrderBy(kind => kind.Order)
            .ToArray();

        public static MapFeatureKind Find(string field)
        {
            foreach (var kind in All)
            {
                if (kind.Field == field)
                {
                    return kind;
                }
            }

            throw new ArgumentException($"No map feature kind for spec list '{field}'.");
        }

        // Notes are the one list of plain strings.
        private static bool IsFeatureList(FieldInfo field)
        {
            var type = field.FieldType;
            if (!type.IsGenericType || type.GetGenericTypeDefinition() != typeof(List<>))
            {
                return false;
            }

            return type.GetGenericArguments()[0] != typeof(string);
        }
    }
}
