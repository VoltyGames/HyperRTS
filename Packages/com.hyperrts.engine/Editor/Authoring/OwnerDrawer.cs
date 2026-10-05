using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Match;
using UnityEditor;
using UnityEngine;

namespace HyperRTS.Editor.Authoring
{
    /// <summary>Draws <see cref="OwnerAttribute"/> fields as a player dropdown with the owner's colour beside it.</summary>
    [CustomPropertyDrawer(typeof(OwnerAttribute))]
    public sealed class OwnerDrawer : PropertyDrawer
    {
        private const float Swatch = 16f;
        private const float Gap = 2f;

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var match = SceneMatch.Current;
            var slots = match != null ? Mathf.Max(match.players.Count, property.intValue) : OwnerAttribute.Max;
            var labels = new GUIContent[slots + 1];
            var values = new int[slots + 1];
            for (var i = 0; i <= slots; i++)
            {
                values[i] = i;
                labels[i] = new GUIContent(Label(match, i));
            }

            var field = new Rect(position.x, position.y, position.width - Swatch - Gap, position.height);
            EditorGUI.IntPopup(field, property, labels, values, label);
            EditorGUI.DrawRect(new Rect(field.xMax + Gap, position.y, Swatch, position.height),
                SceneMatch.PlayerColor(property.intValue));
        }

        private static string Label(MatchAuthoring match, int owner)
        {
            if (owner == 0)
            {
                return "0. Neutral";
            }

            if (match == null)
            {
                return $"{owner}. Player {owner}";
            }

            return owner <= match.players.Count ? $"{owner}. {match.players[owner - 1].name}" : $"{owner}. (no player slot)";
        }
    }
}
