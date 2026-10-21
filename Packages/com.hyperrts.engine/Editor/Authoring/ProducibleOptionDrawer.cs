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
            var row = EditorAssets.Layout(typeof(ProducibleOptionDrawer)).Instantiate();
            var field = row.Require<ObjectField>("option");
            field.objectType = OptionType();
            field.BindProperty(property);
            var icon = row.Require<Image>("icon");
            var summary = row.Require<Label>("summary");

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
