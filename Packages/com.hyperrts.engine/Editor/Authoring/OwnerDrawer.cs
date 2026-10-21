using System.Collections.Generic;
using HyperRTS.Editor.Common;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Match;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace HyperRTS.Editor.Authoring
{
    /// <summary>Draws <see cref="OwnerAttribute"/> fields as a player dropdown with the owner's colour beside it.</summary>
    [CustomPropertyDrawer(typeof(OwnerAttribute))]
    public sealed class OwnerDrawer : PropertyDrawer
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var row = EditorAssets.Layout(typeof(OwnerDrawer)).Instantiate();
            var field = row.Require<DropdownField>("owner");
            field.label = property.displayName;
            var swatch = row.Require<VisualElement>("swatch");

            // Owners run 0 to the last slot, so a choice's index is its owner.
            void Show()
            {
                field.choices = Choices(property.intValue).ConvertAll(Label);
                field.showMixedValue = property.hasMultipleDifferentValues;
                field.SetValueWithoutNotify(field.choices[property.intValue]);
                swatch.style.backgroundColor = SceneMatch.PlayerColor(property.intValue);
            }

            field.RegisterValueChangedCallback(change =>
            {
                property.intValue = field.index;
                property.serializedObject.ApplyModifiedProperties();
                Show();
            });

            // Undo, other inspectors and Match edits (renamed or added players) change what the field shows.
            row.TrackPropertyValue(property, _ => Show());
            row.RegisterCallback<AttachToPanelEvent>(_ => EditorApplication.hierarchyChanged += Show);
            row.RegisterCallback<DetachFromPanelEvent>(_ => EditorApplication.hierarchyChanged -= Show);
            Show();
            return row;
        }

        // Every player slot of the scene's Match, plus the current value when it has no slot.
        private static List<int> Choices(int current)
        {
            var match = SceneMatch.Current;
            var slots = match != null ? Mathf.Max(match.players.Count, current) : Mathf.Max(Faction.Max, current);
            var choices = new List<int>(slots + 1);
            for (var i = 0; i <= slots; i++)
            {
                choices.Add(i);
            }

            return choices;
        }

        private static string Label(int owner)
        {
            if (owner == 0)
            {
                return "0. Neutral";
            }

            var match = SceneMatch.Current;
            if (match == null)
            {
                return $"{owner}. Player {owner}";
            }

            return owner <= match.players.Count ? $"{owner}. {match.players[owner - 1].name}" : $"{owner}. (no player slot)";
        }
    }
}
