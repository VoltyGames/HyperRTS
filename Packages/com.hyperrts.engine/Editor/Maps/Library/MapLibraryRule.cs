using System;
using System.Linq;
using System.Reflection;
using HyperRTS.Editor.Validation;
using HyperRTS.Editor.Validation.Rules;
using UnityEditor;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Library
{
    /// <summary>
    /// The map library loads: every item asset is named <c>&lt;Key&gt;.&lt;type&gt;</c> with a key of its own, its
    /// load checks pass, every <see cref="MapKeyAttribute"/> field names an item that exists and no asset link is broken.
    /// </summary>
    public sealed class MapLibraryRule : IAssetRule
    {
        public void Check(AssetSet assets, ValidationIssues issues)
        {
            MapLibrary library;
            try
            {
                library = MapLibrary.Load();
            }
            catch (InvalidOperationException e)
            {
                issues.Error(null, e.Message);
                return;
            }

            Check(library, issues);
        }

        /// <summary>Checks every item of <paramref name="library"/>, which tests make from items in memory.</summary>
        public static void Check(MapLibrary library, ValidationIssues issues)
        {
            foreach (var type in MapLibrary.ItemTypes)
            {
                foreach (var key in library.Keys(type))
                {
                    CheckItem(library, type, key, library.Original(type, key), issues);
                }
            }
        }

        private static void CheckItem(MapLibrary library, Type type, string key, MapLibraryItem item, ValidationIssues issues)
        {
            try
            {
                library.Get(type, key, null);
            }
            catch (InvalidOperationException e)
            {
                issues.Error(item, e.Message);
            }

            MapLibraryFields.Walk(item, (field, text) => CheckKey(library, field, text, item, issues));
            CheckLinks(item, issues);
        }

        private static void CheckKey(MapLibrary library, FieldInfo field, string text, MapLibraryItem item, ValidationIssues issues)
        {
            var key = field.GetCustomAttribute<MapKeyAttribute>();
            if (key == null || string.IsNullOrEmpty(text) || key.Types.Any(type => library.Contains(type, text)))
            {
                return;
            }

            var types = string.Join(" or ", key.Types.Select(MapLibrary.TypeName));
            issues.Error(item, $"{item.name}: {field.Name} names {types} '{text}', which is not in the map library.");
        }

        // A link to a deleted or moved-away asset keeps its id but loads as null.
        private static void CheckLinks(MapLibraryItem item, ValidationIssues issues)
        {
            var property = new SerializedObject(item).GetIterator();
            while (property.Next(true))
            {
                var broken = property.propertyType == SerializedPropertyType.ObjectReference
                             && property.objectReferenceValue == null
                             && property.objectReferenceEntityIdValue != EntityId.None;
                if (broken)
                {
                    issues.Error(item, $"{item.name}: {property.propertyPath} links an asset that no longer exists.");
                }
            }
        }
    }
}
