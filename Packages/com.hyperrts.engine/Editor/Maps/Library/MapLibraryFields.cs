using System;
using System.Collections;
using System.Reflection;
using HyperRTS.Editor.Common;

namespace HyperRTS.Editor.Maps.Library
{
    /// <summary>
    /// Walks the serialized strings of an item or a spec (nested classes and lists included) and hands each to a
    /// visitor with its field and JSON path (<c>rivers[0].crossings[1].prop</c>), so callers can follow
    /// <see cref="MapKeyAttribute"/> fields.
    /// </summary>
    public static class MapLibraryFields
    {
        public static void Walk(object target, Action<FieldInfo, string, string> visit) => Walk(target, string.Empty, visit);

        private static void Walk(object target, string at, Action<FieldInfo, string, string> visit)
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

        private static void Visit(FieldInfo field, string path, object value, Action<FieldInfo, string, string> visit)
        {
            switch (value)
            {
                case null:
                    return;
                case string text:
                    visit(field, path, text);
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
