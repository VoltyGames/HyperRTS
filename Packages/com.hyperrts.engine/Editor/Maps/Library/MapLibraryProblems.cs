using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;

namespace HyperRTS.Editor.Maps.Library
{
    /// <summary>
    /// The library's load checks, collected instead of thrown: the files index, every item parses, every
    /// <see cref="MapKeyAttribute"/> field names an item that exists and every <see cref="MapAssetAttribute"/> path
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
                    Check(library, library.FileOf(type, key), type, problems);
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

            void Visit(FieldInfo field, string value) => CheckValue(library, field, value, file, problems);
            MapLibraryFields.Walk(read.Item, Visit);
            MapLibraryFields.Walk(read.Refs, Visit);
        }

        private static void CheckValue(MapLibrary library, FieldInfo field, string value, string file,
            List<MapLibraryProblem> problems)
        {
            if (string.IsNullOrEmpty(value))
            {
                return;
            }

            var key = field.GetCustomAttribute<MapKeyAttribute>();
            if (key != null && !key.Types.Any(type => library.Contains(type, value)))
            {
                var types = string.Join(" or ", key.Types.Select(MapLibrary.TypeName));
                problems.Add(new MapLibraryProblem(file, $"{field.Name} names {types} '{value}', which is not in the library."));
            }

            var asset = field.GetCustomAttribute<MapAssetAttribute>();
            if (asset != null && AssetDatabase.LoadAssetAtPath(value, asset.Type) == null)
            {
                problems.Add(new MapLibraryProblem(file, $"{field.Name} '{value}' is not a {asset.Type.Name} asset."));
            }
        }
    }
}
