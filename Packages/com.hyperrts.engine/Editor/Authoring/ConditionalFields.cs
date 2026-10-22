using System;
using System.Collections.Generic;
using System.Reflection;
using HyperRTS.Presentation.HUD;
using HyperRTS.Simulation.Common;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace HyperRTS.Editor.Authoring
{
    /// <summary>Hides <see cref="ShowIfAttribute"/> fields while their condition is false, updating as it changes.</summary>
    public static class ConditionalFields
    {
        private const BindingFlags InstanceFields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        /// <summary>Wires every <c>[ShowIf]</c> field drawn in <paramref name="root"/>, nested blocks too.</summary>
        public static void Bind(VisualElement root, SerializedObject serializedObject)
        {
            var type = serializedObject.targetObject.GetType();
            foreach (var (field, condition) in Conditions(type))
            {
                BindField(root, serializedObject, type, field, condition);
            }
        }

        /// <summary>Each <c>[ShowIf]</c> field's property path with its condition's, which names a sibling field.</summary>
        public static IEnumerable<(string Field, string Condition)> Conditions(Type type, string prefix = "")
        {
            foreach (var field in type.GetFields(InstanceFields))
            {
                var showIf = field.GetCustomAttribute<ShowIfAttribute>();
                if (showIf != null)
                {
                    yield return (prefix + field.Name, prefix + showIf.Field);
                }
                else if (IsNestedBlock(field))
                {
                    foreach (var nested in Conditions(field.FieldType, $"{prefix}{field.Name}."))
                    {
                        yield return nested;
                    }
                }
            }
        }

        /// <summary>True when the condition field is set: a true bool, a number above 0 or an assigned object.</summary>
        public static bool IsMet(SerializedProperty condition)
        {
            // A multi-selection that disagrees keeps the field visible so every object can still be edited.
            if (condition.hasMultipleDifferentValues)
            {
                return true;
            }

            return condition.propertyType switch
            {
                SerializedPropertyType.Boolean => condition.boolValue,
                SerializedPropertyType.Integer => condition.intValue > 0,
                SerializedPropertyType.Float => condition.floatValue > 0f,
                SerializedPropertyType.ObjectReference => condition.objectReferenceValue != null,
                _ => throw new ArgumentException(
                    $"[ShowIf] condition '{condition.propertyPath}' is a {condition.propertyType}; " +
                    "use a bool, a number or an object reference."),
            };
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

        private static void BindField(VisualElement root, SerializedObject serializedObject, Type type, string path,
            string conditionPath)
        {
            var condition = serializedObject.FindProperty(conditionPath) ?? throw new ArgumentException(
                $"[ShowIf] on {type.Name}.{path} names '{conditionPath}', which is not a serialized field.");
            if (TryTrack(root, path, condition))
            {
                return;
            }

            // A nested block draws its fields only once the inspector binds, so wait for a layout that has them.
            EventCallback<GeometryChangedEvent> late = null;
            late = _ =>
            {
                if (TryTrack(root, path, condition))
                {
                    root.UnregisterCallback(late);
                }
            };
            root.RegisterCallback(late);
        }

        private static bool TryTrack(VisualElement root, string path, SerializedProperty condition)
        {
            var field = root.Query<PropertyField>().Where(drawn => drawn.bindingPath == path).First();
            if (field == null)
            {
                return false;
            }

            Apply(field, condition);
            field.TrackPropertyValue(condition, changed => Apply(field, changed));
            return true;
        }

        private static void Apply(VisualElement field, SerializedProperty condition) =>
            field.SetHidden(!IsMet(condition));
    }
}
