using HyperRTS.Simulation.AI;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace HyperRTS.Editor.Authoring
{
    /// <summary>One collapsed line per AI difficulty: its name and key numbers, expanding to every field.</summary>
    [CustomPropertyDrawer(typeof(AITuning))]
    public sealed class AITuningDrawer : PropertyDrawer
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var foldout = new Foldout { value = property.isExpanded };
            foldout.RegisterValueChangedCallback(change =>
            {
                // Nested toggles (Use Abilities) send their own change events up through the foldout.
                if (change.target == foldout)
                {
                    property.isExpanded = change.newValue;
                }
            });

            var end = property.GetEndProperty();
            var child = property.Copy();
            for (var enter = true; child.NextVisible(enter) && !SerializedProperty.EqualContents(child, end); enter = false)
            {
                foldout.Add(new PropertyField(child.Copy()));
            }

            void Show(SerializedProperty changed) => foldout.text = Title(changed);
            Show(property);
            foldout.TrackPropertyValue(property, Show);
            return foldout;
        }

        /// <summary>For example "Hard AI · think 1s · waves of 4 · abilities".</summary>
        public static string Title(SerializedProperty property)
        {
            var think = property.FindPropertyRelative(nameof(AITuning.thinkInterval)).floatValue;
            var wave = property.FindPropertyRelative(nameof(AITuning.attackWaveSize)).intValue;
            var abilities = property.FindPropertyRelative(nameof(AITuning.useAbilities)).boolValue;
            return $"{property.displayName} · think {think:0.##}s · waves of {wave}" + (abilities ? " · abilities" : "");
        }
    }
}
