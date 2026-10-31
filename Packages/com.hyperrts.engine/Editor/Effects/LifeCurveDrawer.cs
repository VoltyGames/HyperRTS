using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace HyperRTS.Editor.Effects
{
    /// <summary>
    /// A <see cref="LifeCurveAttribute"/> list as a curve field over life 0 to 1. Keys are linear, as the builder
    /// reads them; tangents drawn in the curve editor are dropped on save.
    /// </summary>
    [CustomPropertyDrawer(typeof(LifeCurveAttribute))]
    public sealed class LifeCurveDrawer : PropertyDrawer
    {
        private const string TimeField = nameof(CurveKey.time);
        private const string ValueField = nameof(CurveKey.value);

        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var field = new CurveField(property.displayName) { value = Read(property) };
            field.AddToClassList(BaseField<AnimationCurve>.alignedFieldUssClassName);
            field.RegisterValueChangedCallback(change => Write(property, change.newValue));
            field.TrackPropertyValue(property, changed => field.SetValueWithoutNotify(Read(changed)));
            return field;
        }

        private static AnimationCurve Read(SerializedProperty list)
        {
            var curve = new AnimationCurve();
            for (var i = 0; i < list.arraySize; i++)
            {
                var key = list.GetArrayElementAtIndex(i);
                curve.AddKey(key.FindPropertyRelative(TimeField).floatValue, key.FindPropertyRelative(ValueField).floatValue);
            }

            for (var i = 0; i < curve.length; i++)
            {
                AnimationUtility.SetKeyLeftTangentMode(curve, i, AnimationUtility.TangentMode.Linear);
                AnimationUtility.SetKeyRightTangentMode(curve, i, AnimationUtility.TangentMode.Linear);
            }

            return curve;
        }

        private static void Write(SerializedProperty list, AnimationCurve curve)
        {
            list.arraySize = curve.length;
            for (var i = 0; i < curve.length; i++)
            {
                var key = list.GetArrayElementAtIndex(i);
                key.FindPropertyRelative(TimeField).floatValue = curve[i].time;
                key.FindPropertyRelative(ValueField).floatValue = curve[i].value;
            }

            list.serializedObject.ApplyModifiedProperties();
        }
    }
}
