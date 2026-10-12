using System;
using System.Collections.Generic;
using HyperRTS.Editor.Common;
using HyperRTS.Simulation.Production;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace HyperRTS.Editor.Authoring
{
    /// <summary>One production option per row: prefab field, then its icon and "cost · build time" summary.</summary>
    [CustomPropertyDrawer(typeof(ProducibleOptionAttribute))]
    public sealed class ProducibleOptionDrawer : PropertyDrawer
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var row = new VisualElement();
            EditorAssets.AddStyles(row);
            row.AddToClassList("hrts-option");

            var field = new ObjectField { objectType = OptionType(), allowSceneObjects = false };
            field.AddToClassList("hrts-option__field");
            field.BindProperty(property);
            var icon = new Image();
            icon.AddToClassList("hrts-option__icon");
            var summary = new Label();
            summary.AddToClassList("hrts-option__summary");
            row.Add(field);
            row.Add(icon);
            row.Add(summary);

            void Show(SerializedProperty changed)
            {
                var option = changed.objectReferenceValue as ProducibleAuthoring;
                icon.image = EntitySummary.ProducibleIcon(option);
                summary.text = EntitySummary.ProducibleText(option);
                summary.tooltip = summary.text;
            }

            Show(property);
            row.TrackPropertyValue(property, Show);
            return row;
        }

        // The attribute sits on a list, so each element's type is the list's item type.
        private Type OptionType()
        {
            var type = fieldInfo.FieldType;
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>))
            {
                return type.GetGenericArguments()[0];
            }

            if (typeof(ProducibleAuthoring).IsAssignableFrom(type))
            {
                return type;
            }

            throw new InvalidOperationException(
                $"[ProducibleOption] on '{fieldInfo.Name}' needs a ProducibleAuthoring field or list.");
        }
    }
}
