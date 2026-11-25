using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace HyperRTS.Editor.Authoring
{
    /// <summary>Finds attribute-marked serialized fields by property path and waits for the inspector to draw their property fields.</summary>
    public static class DrawnFields
    {
        /// <summary>Each field marked <typeparamref name="T"/>, as its property path and attribute; nested blocks too.</summary>
        public static IEnumerable<(string Path, T Attribute)> Marked<T>(Type type, string prefix = "") where T : Attribute
        {
            foreach (var field in type.GetFields(AuthoringEditor.Members))
            {
                var attribute = field.GetCustomAttribute<T>();
                if (attribute != null)
                {
                    yield return (prefix + field.Name, attribute);
                }
                else if (IsNestedBlock(field))
                {
                    foreach (var nested in Marked<T>(field.FieldType, $"{prefix}{field.Name}."))
                    {
                        yield return nested;
                    }
                }
            }
        }

        /// <summary>
        /// Runs <paramref name="apply"/> on the property field bound to <paramref name="path"/> once it is drawn, and
        /// again after each layout until it returns true: a nested block draws its fields only once the inspector binds.
        /// </summary>
        public static void WhenDrawn(VisualElement root, string path, Func<PropertyField, bool> apply)
        {
            if (TryApply(root, path, apply))
            {
                return;
            }

            EventCallback<GeometryChangedEvent> late = null;
            late = _ =>
            {
                if (TryApply(root, path, apply))
                {
                    root.UnregisterCallback(late);
                }
            };
            root.RegisterCallback(late);
        }

        private static bool IsNestedBlock(FieldInfo field)
        {
            var type = field.FieldType;
            if (!type.IsClass || !type.IsDefined(typeof(SerializableAttribute), false))
            {
                return false;
            }

            if (field.IsNotSerialized || typeof(UnityEngine.Object).IsAssignableFrom(type))
            {
                return false;
            }

            return field.IsPublic || field.IsDefined(typeof(UnityEngine.SerializeField), false);
        }

        private static bool TryApply(VisualElement root, string path, Func<PropertyField, bool> apply)
        {
            var field = root.Query<PropertyField>().Where(drawn => drawn.bindingPath == path).First();
            return field != null && apply(field);
        }
    }
}
