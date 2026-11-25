using HyperRTS.Simulation.Match;
using UnityEditor;
using UnityEngine;

namespace HyperRTS.Editor.Authoring
{
    /// <summary>The Scene view drawing of a Match: its map outline and the nav and fog grid previews.</summary>
    internal static class MatchGizmos
    {
        // Drawing more lines than this would stall the Scene view on fine grids.
        private const int MaxGridLines = 1000;

        public static readonly Color MapColor = new(1f, 0.85f, 0.2f);
        public static readonly Color NavGridColor = new(0.2f, 0.75f, 0.7f, 0.25f);
        public static readonly Color FogGridColor = new(0.6f, 0.6f, 0.25f, 0.35f);

        public static void DrawGrid(Vector3 center, Vector2 size, float cell, Color color)
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

        // Keeps the map visible when the Match isn't selected; the draggable box replaces it when it is.
        [DrawGizmo(GizmoType.NonSelected)]
        private static void DrawMapOutline(MatchAuthoring match, GizmoType type)
        {
            Gizmos.color = GroundHandles.Faded(MapColor, 0.8f);
            Gizmos.DrawWireCube(match.transform.position, new Vector3(match.mapSize.x, 0f, match.mapSize.y));
        }
    }
}
