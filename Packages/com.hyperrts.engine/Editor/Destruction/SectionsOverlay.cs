using System.Collections.Generic;
using HyperRTS.Simulation.Destruction;
using UnityEditor;
using UnityEditor.Overlays;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;

namespace HyperRTS.Editor.Destruction;

/// <summary>
/// Scene view overlay for the selected or open sectioned building: each section's box and name, anchored ones in
/// green, and a line from each section to what holds it. Picking a section in its list highlights it.
/// </summary>
[Overlay(typeof(SceneView), "hyperrts-sections", "Sections")]
public sealed class SectionsOverlay : Overlay, ITransientOverlay
{
    private const int RowHeight = 18;
    private const int MostRows = 12;

    private static readonly Color AnchoredColor = new(0.3f, 0.85f, 0.4f);
    private static readonly Color StandingColor = new(0.4f, 0.75f, 1f);
    private static readonly Color PickedColor = new(1f, 0.85f, 0.2f);

    private readonly List<string> _names = new();
    private readonly ListView _list;
    private SectionsAuthoring _building;
    private int _picked = -1;

    public SectionsOverlay()
    {
        _list = new ListView
        {
            itemsSource = _names,
            selectionType = SelectionType.Single,
            fixedItemHeight = RowHeight,
            makeItem = () => new Label(),
            bindItem = (row, index) => ((Label)row).text = _names[index],
        };
        _list.style.minWidth = 200;
        _list.selectedIndicesChanged += indices =>
        {
            _picked = -1;
            foreach (var index in indices)
            {
                _picked = index;
            }

            SceneView.RepaintAll();
        };
    }

    public bool visible => _building != null;

    public override VisualElement CreatePanelContent() => _list;

    public override void OnCreated()
    {
        Selection.selectionChanged += FindBuilding;
        PrefabStage.prefabStageOpened += OnStageChanged;
        PrefabStage.prefabStageClosing += OnStageChanged;
        SceneView.duringSceneGui += Draw;
        FindBuilding();
    }

    public override void OnWillBeDestroyed()
    {
        Selection.selectionChanged -= FindBuilding;
        PrefabStage.prefabStageOpened -= OnStageChanged;
        PrefabStage.prefabStageClosing -= OnStageChanged;
        SceneView.duringSceneGui -= Draw;
    }

    private void OnStageChanged(PrefabStage stage) => FindBuilding();

    private void FindBuilding()
    {
        var found = Selection.activeGameObject != null
            ? Selection.activeGameObject.GetComponentInParent<SectionsAuthoring>()
            : null;
        var stage = PrefabStageUtility.GetCurrentPrefabStage();
        if (found == null && stage != null)
        {
            found = stage.prefabContentsRoot.GetComponent<SectionsAuthoring>();
        }

        if (found == _building)
        {
            return;
        }

        _building = found;
        ListSections();
    }

    private void ListSections()
    {
        _picked = -1;
        _names.Clear();
        if (_building != null)
        {
            foreach (var entry in _building.sections)
            {
                var name = entry.intact != null ? entry.intact.name : "(no model)";
                _names.Add(entry.anchored ? name + " (anchored)" : name);
            }
        }

        _list.style.height = Mathf.Min(_names.Count, MostRows) * RowHeight;
        _list.ClearSelection();
        _list.Rebuild();
    }

    private void Draw(SceneView view)
    {
        if (view != containerWindow || _building == null || Event.current.type != EventType.Repaint)
        {
            return;
        }

        var entries = _building.sections;
        // A fill or an edit can change the sections while the same building stays picked.
        if (_names.Count != entries.Length)
        {
            ListSections();
        }

        var oldMatrix = Handles.matrix;
        var oldTest = Handles.zTest;
        Handles.matrix = _building.transform.localToWorldMatrix;
        Handles.zTest = UnityEngine.Rendering.CompareFunction.Always;
        for (var i = 0; i < entries.Length; i++)
        {
            DrawHolders(entries, i);
        }

        for (var i = 0; i < entries.Length; i++)
        {
            var entry = entries[i];
            Handles.color = BoxColor(i, entry);
            Handles.DrawWireCube(entry.center, entry.halfSize * 2f);
            Handles.Label(entry.center, _names[i]);
        }

        Handles.matrix = oldMatrix;
        Handles.zTest = oldTest;
    }

    private Color BoxColor(int index, SectionEntry entry)
    {
        if (index == _picked)
        {
            return PickedColor;
        }

        return entry.anchored ? AnchoredColor : StandingColor;
    }

    private void DrawHolders(SectionEntry[] entries, int index)
    {
        var holders = SectionShape.Bits(entries, entries[index].supports);
        Handles.color = index == _picked ? PickedColor : new Color(1f, 1f, 1f, 0.35f);
        for (var holder = 0; holder < entries.Length; holder++)
        {
            if ((holders & (1ul << holder)) != 0)
            {
                Handles.DrawLine(entries[index].center, entries[holder].center);
            }
        }
    }
}
