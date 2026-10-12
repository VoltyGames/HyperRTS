using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using HyperRTS.Editor.Maps.Spec;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Library
{
    /// <summary>
    /// Finds the library items and map specs (<c>*.map.json</c> under the library's root) whose
    /// <see cref="MapKeyAttribute"/> fields name an item.
    /// </summary>
    public static class MapLibraryUsages
    {
        public static List<MapLibraryUsage> Find(MapLibrary library, Type type, string key)
        {
            var usages = new List<MapLibraryUsage>();
            foreach (var itemType in MapLibrary.ItemTypes)
            {
                foreach (var itemKey in library.Keys(itemType))
                {
                    var file = library.FileOf(itemType, itemKey);
                    var (item, refs) = MapLibraryItemFile.Read(itemType, file);
                    var fields = Naming(type, key, item).Concat(Naming(type, key, refs));
                    usages.AddRange(fields.Select(field => new MapLibraryUsage(file, itemType, itemKey, field)));
                }
            }

            foreach (var file in Specs(library.Root))
            {
                var spec = JsonUtility.FromJson<MapSpec>(File.ReadAllText(file));
                usages.AddRange(Naming(type, key, spec).Select(field => new MapLibraryUsage(file, null, null, field)));
            }

            return usages;
        }

        private static IEnumerable<string> Specs(string root) =>
            Directory.EnumerateFiles(root, "*.map.json", SearchOption.AllDirectories)
                .Select(file => file.Replace('\\', '/'))
                .OrderBy(file => file, StringComparer.Ordinal);

        /// <summary>The fields of <paramref name="target"/> that name item <paramref name="key"/> of <paramref name="type"/>.</summary>
        private static List<string> Naming(Type type, string key, object target)
        {
            var fields = new List<string>();
            MapLibraryFields.Walk(target, (field, value) =>
            {
                if (value == key && Names(field, type) && !fields.Contains(field.Name))
                {
                    fields.Add(field.Name);
                }
            });
            return fields;
        }

        private static bool Names(FieldInfo field, Type type)
        {
            var attribute = field.GetCustomAttribute<MapKeyAttribute>();
            return attribute != null && attribute.Types.Contains(type);
        }
    }
}
