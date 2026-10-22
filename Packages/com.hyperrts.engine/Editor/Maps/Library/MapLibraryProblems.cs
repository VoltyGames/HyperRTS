using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace HyperRTS.Editor.Maps.Library
{
    /// <summary>
    /// The library's load checks, collected instead of thrown: the files index, every item parses, every
    /// <see cref="MapKeyAttribute"/> field names an item that exists and every <see cref="MapAssetAttribute"/> link
    /// loads as its asset type.
    /// </summary>
    public static class MapLibraryProblems
    {
        public static List<MapLibraryProblem> Find(string root)
        {
            MapLibrary library;
            try
            {
                library = MapLibrary.Load(root);
            }
            catch (InvalidOperationException e)
            {
                return new List<MapLibraryProblem> { new(null, e.Message) };
            }

            return Find(library);
        }

        public static List<MapLibraryProblem> Find(MapLibrary library)
        {
            var problems = new List<MapLibraryProblem>();
            foreach (var type in MapLibrary.ItemTypes)
            {
                foreach (var key in library.Keys(type))
                {
                    Check(library, library.FilePath(type, key), type, problems);
                }
            }

            return problems;
        }

        private static void Check(MapLibrary library, string file, Type type, List<MapLibraryProblem> problems)
        {
            (object Item, MapLibraryRefs Refs) read;
            try
            {
                read = MapLibraryItemFile.Read(type, file);
            }
            catch (InvalidOperationException e)
            {
                problems.Add(new MapLibraryProblem(file, e.Message));
                return;
            }

            void Visit(FieldInfo field, object value) => CheckValue(library, field, value, file, problems);
            MapLibraryFields.Walk(read.Item, Visit);
            MapLibraryFields.Walk(read.Refs, Visit);
        }

        private static void CheckValue(MapLibrary library, FieldInfo field, object value, string file,
            List<MapLibraryProblem> problems)
        {
            if (value is MapAssetRef link)
            {
                CheckAsset(field, link, file, problems);
                return;
            }

            var key = field.GetCustomAttribute<MapKeyAttribute>();
            var text = (string)value;
            if (key == null || string.IsNullOrEmpty(text) || key.Types.Any(type => library.Contains(type, text)))
            {
                return;
            }

            var types = string.Join(" or ", key.Types.Select(MapLibrary.TypeName));
            problems.Add(new MapLibraryProblem(file, $"{field.Name} names {types} '{text}', which is not in the library."));
        }

        private static void CheckAsset(FieldInfo field, MapAssetRef link, string file, List<MapLibraryProblem> problems)
        {
            var type = field.GetCustomAttribute<MapAssetAttribute>()?.Type
                ?? throw new InvalidOperationException($"HyperRTS: {field.DeclaringType?.Name}.{field.Name} links an asset without [MapAsset].");
            if (link.IsEmpty)
            {
                return;
            }

            link.Load(type, out var problem);
            if (problem != null)
            {
                problems.Add(new MapLibraryProblem(file, $"{field.Name} {problem}."));
            }
        }
    }
}
