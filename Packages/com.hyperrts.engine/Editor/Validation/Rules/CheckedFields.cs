using System;
using System.Collections.Generic;
using System.Reflection;
using Unity.Scripting.LifecycleManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HyperRTS.Editor.Validation.Rules
{
    /// <summary>
    /// A serialized type's fields that carry check attributes, and the nested blocks and lists that hold more.
    /// Rules ask on every component each inspector layout, so each type is reflected once.
    /// </summary>
    internal sealed partial class CheckedFields
    {
        private const BindingFlags Declared =
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

        [AutoStaticsCleanup]
        private static readonly Dictionary<Type, CheckedFields> Cache = new();

        public readonly List<CheckedField> Checked = new();
        public readonly List<(FieldInfo Field, CheckedFields Inner)> Nested = new();

        public bool IsEmpty => Checked.Count == 0 && Nested.Count == 0;

        public static CheckedFields Find(Type type)
        {
            if (Cache.TryGetValue(type, out var fields))
            {
                return fields;
            }

            // Cached before collecting, so a type that holds itself stops the walk.
            Cache[type] = fields = new CheckedFields();
            for (var current = type; current != null && current != typeof(MonoBehaviour); current = current.BaseType)
            {
                foreach (var field in current.GetFields(Declared))
                {
                    fields.Collect(field);
                }
            }

            return fields;
        }

        private void Collect(FieldInfo field)
        {
            if (!IsSerialized(field))
            {
                return;
            }

            var element = ElementType(field.FieldType);
            var check = new CheckedField(field, element);
            if (check.HasChecks)
            {
                Checked.Add(check);
            }

            if (!CanNest(element))
            {
                return;
            }

            var inner = Find(element);
            if (!inner.IsEmpty)
            {
                Nested.Add((field, inner));
            }
        }

        private static bool IsSerialized(FieldInfo field)
        {
            if (field.IsInitOnly || field.IsNotSerialized)
            {
                return false;
            }

            return field.IsPublic || field.IsDefined(typeof(SerializeField));
        }

        private static Type ElementType(Type type)
        {
            if (type.IsArray)
            {
                return type.GetElementType();
            }

            var isList = type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>);
            return isList ? type.GetGenericArguments()[0] : type;
        }

        private static bool CanNest(Type type)
        {
            if (type.IsPrimitive || type.IsEnum || type == typeof(string))
            {
                return false;
            }

            return type.IsSerializable && !typeof(Object).IsAssignableFrom(type);
        }
    }
}
