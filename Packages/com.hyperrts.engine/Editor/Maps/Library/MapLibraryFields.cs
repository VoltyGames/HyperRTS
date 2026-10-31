using System;
using System.Collections;
using System.Reflection;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Library
{
    /// <summary>
    /// Walks the serialized strings of an item or a spec (nested classes and lists included) and hands each to a
    /// visitor with its field, so callers can follow <see cref="MapKeyAttribute"/> fields.
    /// </summary>
    public static class MapLibraryFields
    {
        public static void Walk(object target, Action<FieldInfo, string> visit)
        {
            foreach (var field in target.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                if (!IsSerialized(field))
                {
                    continue;
                }

                Visit(field, field.GetValue(target), visit);
            }
        }

        private static void Visit(FieldInfo field, object value, Action<FieldInfo, string> visit)
        {
            switch (value)
            {
                case null:
                    return;
                case string text:
                    visit(field, text);
                    return;
                case IList list:
                    foreach (var element in list)
                    {
                        Visit(field, element, visit);
                    }

                    return;
            }

            if (IsNested(value.GetType()))
            {
                Walk(value, visit);
            }
        }

        private static bool IsSerialized(FieldInfo field)
        {
            if (field.IsDefined(typeof(NonSerializedAttribute)))
            {
                return false;
            }

            return field.IsPublic || field.IsDefined(typeof(SerializeField));
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
