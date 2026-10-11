using HyperRTS.Simulation.GameEntities;
using HyperRTS.Simulation.Match;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

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

        private VisualElement _owners;

        protected override void BuildFooter(VisualElement root)
        {
            var section = new VisualElement();
            section.AddToClassList("hrts-section");
            var title = new Label("Open scenes");
            title.AddToClassList("hrts-section__title");
            section.Add(title);
            _owners = new VisualElement();
            section.Add(_owners);

            var buttons = new VisualElement();
            buttons.AddToClassList("hrts-buttons");
            buttons.Add(GridToggle("Nav Grid", _showNavGrid, value => _showNavGrid = value));
            buttons.Add(GridToggle("Fog Grid", _showFogGrid, value => _showFogGrid = value));
            buttons.Add(new Button(FrameMap) { text = "Frame Map" });
            section.Add(buttons);
            root.Add(section);
        }

        // A full scene scan, so it runs on edits and hierarchy changes, never per frame.
        protected override void Refresh()
        {
            var match = (MatchAuthoring)target;
            var counts = CountOwners(match.players.Count);
            _owners.Clear();
            _owners.Add(OwnerRow(Color.grey, "Neutral", counts[0]));
            for (var i = 0; i < match.players.Count; i++)
            {
                var player = match.players[i];
                var name = $"{i + 1}. {player.name} (team {player.team}, {player.control})";
                _owners.Add(OwnerRow(player.color, name, counts[i + 1]));
            }
        }

        private static VisualElement OwnerRow(Color color, string name, int count)
        {
            var row = new VisualElement();
            row.AddToClassList("hrts-row");
            var swatch = new VisualElement();
            swatch.AddToClassList("hrts-swatch");
            swatch.style.backgroundColor = color;
            row.Add(swatch);
            var label = new Label(name);
            label.AddToClassList("hrts-row__grow");
            row.Add(label);
            row.Add(new Label($"{count} entities"));
            return row;
        }

        private static ToolbarToggle GridToggle(string text, bool value, System.Action<bool> set)
        {
            var toggle = new ToolbarToggle { text = text, value = value };
            toggle.RegisterValueChangedCallback(change =>
            {
                set(change.newValue);
                SceneView.RepaintAll();
            });
            return toggle;
        }

        private void FrameMap()
        {
            var match = (MatchAuthoring)target;
            var size = new Vector3(match.mapSize.x, 1f, match.mapSize.y);
            SceneView.lastActiveSceneView?.Frame(new Bounds(match.transform.position, size), false);
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
