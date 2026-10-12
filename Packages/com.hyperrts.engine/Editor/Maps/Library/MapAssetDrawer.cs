using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace HyperRTS.Editor.Maps.Library
{
    /// <summary>Shows a <see cref="MapAssetAttribute"/> path as an object field and writes the picked asset's path back.</summary>
    [CustomPropertyDrawer(typeof(MapAssetAttribute))]
    public sealed class MapAssetDrawer : PropertyDrawer
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var type = ((MapAssetAttribute)attribute).Type;
            var field = new ObjectField(property.displayName)
            {
                objectType = type,
                allowSceneObjects = false,
                tooltip = property.tooltip,
            };
            field.AddToClassList(BaseField<Object>.alignedFieldUssClassName);
            Show(field, property.stringValue);
            field.RegisterValueChangedCallback(change =>
            {
                property.stringValue = change.newValue == null ? string.Empty : AssetDatabase.GetAssetPath(change.newValue);
                property.serializedObject.ApplyModifiedProperties();
            });
            field.TrackPropertyValue(property, changed => Show(field, changed.stringValue));
            return field;
        }

        private static void Show(ObjectField field, string path)
        {
            var asset = string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath(path, field.objectType);
            field.SetValueWithoutNotify(asset);
            field.EnableInClassList("hrts-library-asset--missing", asset == null && !string.IsNullOrEmpty(path));
        }
    }
}
