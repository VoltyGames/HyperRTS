using System;
using System.Collections.Generic;
using System.Linq;
using HyperRTS.Editor.Common;
using HyperRTS.Presentation.HUD;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace HyperRTS.Editor.Maps.Library
{
    /// <summary>Draws <see cref="MapKeyAttribute"/> string fields as a dropdown of the library's keys of those types.</summary>
    [CustomPropertyDrawer(typeof(MapKeyAttribute))]
    public sealed class MapKeyDrawer : PropertyDrawer
    {
        private const string None = "(none)";

        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var root = EditorAssets.Layout(typeof(MapKeyDrawer)).Instantiate();
            var field = root.Require<DropdownField>("key");
            field.label = property.displayName;
            var types = ((MapKeyAttribute)attribute).Types;

            void Show()
            {
                field.choices = Choices(types, property.stringValue);
                field.showMixedValue = property.hasMultipleDifferentValues;
                field.SetValueWithoutNotify(string.IsNullOrEmpty(property.stringValue) ? None : property.stringValue);
            }

            field.RegisterValueChangedCallback(change =>
            {
                property.stringValue = change.newValue == None ? string.Empty : change.newValue;
                property.serializedObject.ApplyModifiedProperties();
            });
            root.TrackPropertyValue(property, _ => Show());
            Show();
            return root;
        }

        // The library's keys of every type the field takes, plus the current value when no item has it, so a bad key stays visible.
        private static List<string> Choices(Type[] types, string current)
        {
            var keys = types.SelectMany(type => AssetDatabase.FindAssets("t:" + type.Name)
                    .Select(AssetDatabase.GUIDToAssetPath)
                    .Select(MapLibrary.ParseFile)
                    .Where(parsed => parsed.Type == type)
                    .Select(parsed => parsed.Key))
                .Distinct()
                .OrderBy(key => key, StringComparer.Ordinal)
                .ToList();
            if (!string.IsNullOrEmpty(current) && !keys.Contains(current))
            {
                keys.Add(current);
            }

            keys.Insert(0, None);
            return keys;
        }
    }
}
