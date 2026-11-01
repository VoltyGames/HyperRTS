using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using HyperRTS.Editor.Common;
using HyperRTS.Editor.Maps.Library;
using HyperRTS.Editor.Maps.Spec;

namespace HyperRTS.Editor.Maps.Tools
{
    /// <summary>Writes the JSON Schema of <c>.map.json</c>, each <see cref="MapKeyAttribute"/> field listing the library's keys.</summary>
    public static class MapSpecSchema
    {
        public static void Write(string path, MapLibrary library) => File.WriteAllText(path, Text(library));

        /// <summary>The schema <see cref="Write"/> writes, so a test can compare it with the committed file.</summary>
        public static string Text(MapLibrary library) =>
            JsonFormat.Write(JsonSpecSchema.Build(typeof(MapSpec), "Map spec", field => Keys(library, field)));

        private static IReadOnlyList<string> Keys(MapLibrary library, FieldInfo field)
        {
            var key = field.GetCustomAttribute<MapKeyAttribute>();
            if (key == null)
            {
                return null;
            }

            var keys = key.Types.SelectMany(type => library.Keys(type)).Distinct().OrderBy(k => k, StringComparer.Ordinal);
            return key.Optional ? keys.Prepend(string.Empty).ToList() : keys.ToList();
        }
    }
}
