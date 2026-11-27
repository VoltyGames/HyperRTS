using HyperRTS.Editor.Common;
using HyperRTS.Presentation.HUD;
using HyperRTS.Simulation.GameEntities;
using HyperRTS.Simulation.Match;
using Unity.Scripting.LifecycleManagement;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace HyperRTS.Editor.Authoring;

/// <summary>Match inspector: entities per player, map framing, a draggable map boundary and a grid preview.</summary>
[CustomEditor(typeof(MatchAuthoring))]
public class MatchAuthoringEditor : AuthoringEditor
{
    [NoAutoStaticsCleanup]
    private static bool _showNavGrid;
    [NoAutoStaticsCleanup]
    private static bool _showFogGrid;

    private VisualElement _owners;
    private VisualTreeAsset _ownerRow;

    protected override void BuildFooter(VisualElement footer)
    {
        EditorAssets.Layout(typeof(MatchAuthoringEditor)).CloneTree(footer);
        _owners = footer.Require<VisualElement>("owners");
        _ownerRow = EditorAssets.Layout(typeof(MatchAuthoringEditor), "Owner");
        BindGridToggle(footer.Require<ToolbarToggle>("nav-grid"), _showNavGrid, value => _showNavGrid = value);
        BindGridToggle(footer.Require<ToolbarToggle>("fog-grid"), _showFogGrid, value => _showFogGrid = value);
        footer.Require<Button>("frame-map").clicked += FrameMap;
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

    private VisualElement OwnerRow(Color color, string name, int count)
    {
        var row = _ownerRow.CloneRoot();
        row.Require<VisualElement>("swatch").style.backgroundColor = color;
        row.Require<Label>("name").text = name;
        row.Require<Label>("count").text = $"{count} entities";
        return row;
    }

    private static void BindGridToggle(ToolbarToggle toggle, bool value, System.Action<bool> set)
    {
        toggle.value = value;
        toggle.RegisterValueChangedCallback(change =>
        {
            set(change.newValue);
            SceneView.RepaintAll();
        });
    }

    private void FrameMap()
    {
        var match = (MatchAuthoring)target;
        var size = new Vector3(match.mapSize.x, 1f, match.mapSize.y);
        var view = SceneView.lastActiveSceneView;
        if (view != null)
        {
            view.Frame(new Bounds(match.transform.position, size), false);
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

    private void OnSceneGUI()
    {
        var match = (MatchAuthoring)target;
        var center = match.transform.position;

        if (_showNavGrid)
        {
            MatchGizmos.DrawGrid(center, match.mapSize, match.navCellSize, MatchGizmos.NavGridColor);
        }

        if (_showFogGrid)
        {
            MatchGizmos.DrawGrid(center, match.mapSize, match.fogCellSize, MatchGizmos.FogGridColor);
        }

        GroundHandles.EditBox(match, center, match.mapSize, MatchGizmos.MapColor, "Resize Map", size => match.mapSize = size);
    }
}
