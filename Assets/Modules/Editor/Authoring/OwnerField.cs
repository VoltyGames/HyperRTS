using HyperRTS.Simulation.Match;
using UnityEditor;
using UnityEngine;

namespace HyperRTS.Editor.Authoring
{
    /// <summary>Owner number as a dropdown of the scene Match's players, with the owner's colour beside it.</summary>
    public static class OwnerField
    {
        private const int MaxPlayers = 15;

        public static void Draw(SerializedProperty property)
        {
            var match = Object.FindAnyObjectByType<MatchAuthoring>();
            var slots = match != null ? Mathf.Max(match.players.Count, property.intValue) : MaxPlayers;
            var labels = new GUIContent[slots + 1];
            var values = new int[slots + 1];
            for (var i = 0; i <= slots; i++)
            {
                values[i] = i;
                labels[i] = new GUIContent(Label(match, i));
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.IntPopup(property, labels, values, new GUIContent(property.displayName, property.tooltip));
                var swatch = GUILayoutUtility.GetRect(16f, EditorGUIUtility.singleLineHeight, GUILayout.Width(16f));
                EditorGUI.DrawRect(swatch, ColorOf(match, property.intValue));
            }
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

        private static Color ColorOf(MatchAuthoring match, int owner) =>
            owner > 0 && match != null && owner <= match.players.Count ? match.players[owner - 1].color : Color.grey;
    }
}
