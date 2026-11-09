using System;
using System.Collections;
using System.Reflection;
using HyperRTS.Editor.Common;

namespace HyperRTS.Editor.Maps.Library
{
    /// <summary>
    /// Walks the serialized strings and numbers of an item or a spec and hands each to a visitor with its field and
    /// JSON path, so callers can follow <see cref="MapKeyAttribute"/>, <see cref="AboveAttribute"/> and Unity
    /// <c>[Min]</c> fields.
    /// </summary>
    public static class MapLibraryFields
    {
        public static void Walk(object target, Action<FieldInfo, string, object> visit) => Walk(target, string.Empty, visit);

        /// <summary>Walks from <paramref name="at"/>, the path prefix that names where <paramref name="target"/> lies.</summary>
        public static void Walk(object target, string at, Action<FieldInfo, string, object> visit)
        {
            foreach (var field in target.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                if (!UnitySerialization.IsSerialized(field))
                {
                    continue;
                }

                var path = at.Length == 0 ? field.Name : $"{at}.{field.Name}";
                Visit(field, path, field.GetValue(target), visit);
            }
        }

        private static void Visit(FieldInfo field, string path, object value, Action<FieldInfo, string, object> visit)
        {
            switch (value)
            {
                case null:
                    return;
                case string or int or float:
                    visit(field, path, value);
                    return;
                case IList list:
                    for (var i = 0; i < list.Count; i++)
                    {
                        Visit(field, $"{path}[{i}]", list[i], visit);
                    }

                    return;
            }

            if (IsNested(value.GetType()))
            {
                Walk(value, path, visit);
            }
        }

        private static bool IsNested(Type type)
        {
            if (!type.IsClass || typeof(UnityEngine.Object).IsAssignableFrom(type))
            {
                return false;
            }

            return type.IsDefined(typeof(SerializableAttribute), false);
        }
    }
}
