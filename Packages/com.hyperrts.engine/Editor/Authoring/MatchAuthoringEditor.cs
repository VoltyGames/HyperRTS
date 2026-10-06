using HyperRTS.Simulation.GameEntities;
using HyperRTS.Simulation.Match;
using UnityEditor;
using UnityEngine;

namespace HyperRTS.Editor.Authoring
{
    /// <summary>Match inspector: entities per player, map framing, a draggable map boundary and a grid preview.</summary>
    [CustomEditor(typeof(MatchAuthoring))]
    public class MatchAuthoringEditor : AuthoringEditor
    {
        // Drawing more lines than this would stall the Scene view on fine grids.
        private const int MaxGridLines = 1000;

        private static readonly Color MapColor = new(1f, 0.85f, 0.2f);

        private static bool _showNavGrid;
        private static bool _showFogGrid;

        private int[] _counts = { };

        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();
            var match = (MatchAuthoring)target;

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Open scenes", EditorStyles.boldLabel);
            DrawOwnerCounts(match);

            using (new EditorGUILayout.HorizontalScope())
            {
                _showNavGrid = GUILayout.Toggle(_showNavGrid, "Nav Grid", "Button");
                _showFogGrid = GUILayout.Toggle(_showFogGrid, "Fog Grid", "Button");
                if (GUILayout.Button("Frame Map"))
                {
                    var size = new Vector3(match.mapSize.x, 1f, match.mapSize.y);
                    SceneView.lastActiveSceneView?.Frame(new Bounds(match.transform.position, size), false);
                }
            }

            if (GUI.changed)
            {
                SceneView.RepaintAll();
            }
        }

        private void DrawOwnerCounts(MatchAuthoring match)
        {
            // A full scene scan, so only on Layout; Repaint and input events reuse it.
            if (Event.current.type == EventType.Layout || _counts.Length != match.players.Count + 1)
            {
                _counts = CountOwners(match.players.Count);
            }

            EditorGUILayout.LabelField("Neutral", $"{_counts[0]} entities");
            for (var i = 0; i < match.players.Count; i++)
            {
                var player = match.players[i];
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUI.DrawRect(GUILayoutUtility.GetRect(12f, 16f, GUILayout.Width(12f)), player.color);
                    EditorGUILayout.LabelField($"{i + 1}. {player.name} (team {player.team}, {player.control})",
                        $"{_counts[i + 1]} entities");
                }
            }
        }

        private static int[] CountOwners(int players)
        {
            var counts = new int[players + 1];
            foreach (var entity in FindObjectsByType<GameEntityAuthoring>(FindObjectsInactive.Include))
            {
                if (entity.owner < counts.Length)
                {
                    counts[entity.owner]++;
                }
            }

            return counts;
        }

        // Keeps the map visible when the Match isn't selected; the draggable box replaces it when it is.
        [DrawGizmo(GizmoType.NonSelected)]
        private static void DrawMapOutline(MatchAuthoring match, GizmoType type)
        {
            Gizmos.color = GroundHandles.Faded(MapColor, 0.8f);
            Gizmos.DrawWireCube(match.transform.position, new Vector3(match.mapSize.x, 0f, match.mapSize.y));
        }

        private void OnSceneGUI()
        {
            var match = (MatchAuthoring)target;
            var center = match.transform.position;

            if (_showNavGrid)
            {
                DrawGrid(center, match.mapSize, match.navCellSize, new Color(0.2f, 0.75f, 0.7f, 0.25f));
            }

            if (_showFogGrid)
            {
                DrawGrid(center, match.mapSize, match.fogCellSize, new Color(0.6f, 0.6f, 0.25f, 0.35f));
            }

            GroundHandles.EditBox(match, center, match.mapSize, MapColor, "Resize Map", size => match.mapSize = size);
        }

        private static void DrawGrid(Vector3 center, Vector2 size, float cell, Color color)
        {
            var lines = Mathf.CeilToInt(size.x / cell) + Mathf.CeilToInt(size.y / cell);
            if (cell <= 0f || lines > MaxGridLines)
            {
                Handles.Label(center, $"Grid too fine to preview ({lines} lines)");
                return;
            }

            var min = center - new Vector3(size.x, 0f, size.y) * 0.5f;
            Handles.color = color;
            for (var x = 0f; x <= size.x; x += cell)
            {
                Handles.DrawLine(min + new Vector3(x, 0f, 0f), min + new Vector3(x, 0f, size.y));
            }

            for (var z = 0f; z <= size.y; z += cell)
            {
                Handles.DrawLine(min + new Vector3(0f, 0f, z), min + new Vector3(size.x, 0f, z));
            }
        }
    }
}
