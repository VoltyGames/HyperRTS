using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace HyperRTS.Editor.Effects
{
    /// <summary>
    /// An <see cref="EffectAssetAttribute"/> path as an object picker. A picked asset is written back as its path;
    /// a path that names no asset shows as missing, and the Validate page reports it.
    /// </summary>
    [CustomPropertyDrawer(typeof(EffectAssetAttribute))]
    public sealed class EffectAssetDrawer : PropertyDrawer
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var type = ((EffectAssetAttribute)attribute).Type;
            var field = new ObjectField(property.displayName) { objectType = type, allowSceneObjects = false, value = Load(property.stringValue, type) };
            field.AddToClassList(ObjectField.alignedFieldUssClassName);
            field.RegisterValueChangedCallback(change =>
            {
                property.stringValue = change.newValue == null ? string.Empty : AssetDatabase.GetAssetPath(change.newValue);
                property.serializedObject.ApplyModifiedProperties();
            });
            field.TrackPropertyValue(property, changed => field.SetValueWithoutNotify(Load(changed.stringValue, type)));
            return field;
        }

        // Recipes may name an asset by GUID as well as by path, as EffectLayer.Load accepts.
        private static Object Load(string reference, System.Type type)
        {
            var path = Regex.IsMatch(reference ?? "", "^[0-9a-f]{32}$") ? AssetDatabase.GUIDToAssetPath(reference) : reference;
            return string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath(path, type);
        }
    }
}
