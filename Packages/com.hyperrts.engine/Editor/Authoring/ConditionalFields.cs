using System;
using System.Reflection;
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

        /// <summary>Wires every top-level <c>[ShowIf]</c> field drawn in <paramref name="root"/>.</summary>
        public static void Bind(VisualElement root, SerializedObject serializedObject)
        {
            var type = serializedObject.targetObject.GetType();
            foreach (var field in type.GetFields(InstanceFields))
            {
                var showIf = field.GetCustomAttribute<ShowIfAttribute>();
                if (showIf != null)
                {
                    BindField(root, serializedObject, type, field.Name, showIf.Field);
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

        private static void BindField(VisualElement root, SerializedObject serializedObject, Type type, string name,
            string conditionName)
        {
            var condition = serializedObject.FindProperty(conditionName) ?? throw new ArgumentException(
                $"[ShowIf] on {type.Name}.{name} names '{conditionName}', which is not a serialized field.");
            var field = root.Query<PropertyField>().Where(drawn => drawn.bindingPath == name).First();
            if (field == null)
            {
                return;
            }

            Apply(field, condition);
            field.TrackPropertyValue(condition, changed => Apply(field, changed));
        }

        private static void Apply(VisualElement field, SerializedProperty condition) =>
            field.style.display = IsMet(condition) ? DisplayStyle.Flex : DisplayStyle.None;
    }
}
