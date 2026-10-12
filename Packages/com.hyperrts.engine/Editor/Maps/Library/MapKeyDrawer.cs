using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace HyperRTS.Editor.Maps.Library
{
    /// <summary>
    /// Shows a <see cref="MapKeyAttribute"/> string as a dropdown of the library's keys of its types. A key the library
    /// doesn't hold stays selected and is marked missing, so a typo shows instead of being replaced.
    /// </summary>
    [CustomPropertyDrawer(typeof(MapKeyAttribute))]
    public sealed class MapKeyDrawer : PropertyDrawer
    {
        public const string MissingClass = "hrts-map-key--missing";
        private const string NoneLabel = "(none)";

        private static MapLibrary _library;
        private static int _libraryVersion = -1;

        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            if (property.propertyType != SerializedPropertyType.String)
            {
                throw new InvalidOperationException(
                    $"HyperRTS: [MapKey] on '{property.propertyPath}' needs a string field, not {property.propertyType}.");
            }

            var known = Keys(((MapKeyAttribute)attribute).Types);
            var choices = new List<string> { string.Empty };
            choices.AddRange(known);
            string Format(string key) => Describe(key, known);
            var field = new PopupField<string>(property.displayName, choices, 0, Format, Format)
            {
                tooltip = property.tooltip,
            };
            field.AddToClassList(BaseField<string>.alignedFieldUssClassName);
            Show(field, property.stringValue, known);
            field.RegisterValueChangedCallback(change =>
            {
                property.stringValue = change.newValue;
                property.serializedObject.ApplyModifiedProperties();
            });
            field.TrackPropertyValue(property, changed => Show(field, changed.stringValue, known));
            return field;
        }

        /// <summary>Every key of <paramref name="types"/> in the library, in ordinal order.</summary>
        public static SortedSet<string> Keys(IEnumerable<Type> types)
        {
            var library = Library();
            return new SortedSet<string>(types.SelectMany(type => library.Keys(type)), StringComparer.Ordinal);
        }

        /// <summary>The library as last read; read again after the project changed.</summary>
        private static MapLibrary Library()
        {
            if (_library == null || _libraryVersion != MapLibraryChanges.Version)
            {
                _library = MapLibrary.Load();
                _libraryVersion = MapLibraryChanges.Version;
            }

            return _library;
        }

        private static void Show(PopupField<string> field, string key, ISet<string> known)
        {
            key ??= string.Empty;
            if (!field.choices.Contains(key))
            {
                field.choices.Add(key);
            }

            field.SetValueWithoutNotify(key);
            field.EnableInClassList(MissingClass, IsMissing(key, known));
        }

        private static string Describe(string key, ISet<string> known)
        {
            if (string.IsNullOrEmpty(key))
            {
                return NoneLabel;
            }

            return IsMissing(key, known) ? $"{key} (missing)" : key;
        }

        private static bool IsMissing(string key, ISet<string> known) => !string.IsNullOrEmpty(key) && !known.Contains(key);
    }
}
