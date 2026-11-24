using System.Collections.Generic;
using HyperRTS.Simulation.Destruction;
using UnityEditor;
using UnityEditor.Overlays;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;

namespace HyperRTS.Editor.Destruction
{
    /// <summary>
    /// Scene view overlay for the selected or open sectioned building: each section's box and name, anchored ones in
    /// green, and a line from each section to what holds it. Picking a section in its list highlights it.
    /// </summary>
    [Overlay(typeof(SceneView), "hyperrts-sections", "Sections")]
    public class SectionsOverlay : Overlay, ITransientOverlay
    {
        private const int RowHeight = 18;
        private const int MostRows = 12;

        private static readonly Color AnchoredColor = new(0.3f, 0.85f, 0.4f);
        private static readonly Color StandingColor = new(0.4f, 0.75f, 1f);
        private static readonly Color PickedColor = new(1f, 0.85f, 0.2f);

        private readonly List<string> names = new();
        private readonly ListView list;
        private SectionsAuthoring building;
        private int picked = -1;

        public SectionsOverlay()
        {
            list = new ListView
            {
                itemsSource = names,
                selectionType = SelectionType.Single,
                fixedItemHeight = RowHeight,
                makeItem = () => new Label(),
                bindItem = (row, index) => ((Label)row).text = names[index],
            };
            list.style.minWidth = 200;
            list.selectedIndicesChanged += indices =>
            {
                picked = -1;
                foreach (var index in indices)
                {
                    picked = index;
                }

                SceneView.RepaintAll();
            };
        }

        public bool visible => building != null;

        public override VisualElement CreatePanelContent() => list;

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
            if (found == null)
            {
                found = PrefabStageUtility.GetCurrentPrefabStage()?.prefabContentsRoot
                    .GetComponent<SectionsAuthoring>();
            }

            if (found == building)
            {
                return;
            }

            building = found;
            picked = -1;
            names.Clear();
            if (building != null)
            {
                foreach (var entry in building.sections)
                {
                    var name = entry.intact != null ? entry.intact.name : "(no model)";
                    names.Add(entry.anchored ? name + " (anchored)" : name);
                }
            }

            list.style.height = Mathf.Min(names.Count, MostRows) * RowHeight;
            list.ClearSelection();
            list.Rebuild();
        }

        private void Draw(SceneView view)
        {
            if (view != containerWindow || building == null || Event.current.type != EventType.Repaint)
            {
                return;
            }

            var entries = building.sections;
            var oldMatrix = Handles.matrix;
            var oldTest = Handles.zTest;
            Handles.matrix = building.transform.localToWorldMatrix;
            Handles.zTest = UnityEngine.Rendering.CompareFunction.Always;
            for (var i = 0; i < entries.Length; i++)
            {
                DrawHolders(entries, i);
            }

            for (var i = 0; i < entries.Length; i++)
            {
                var entry = entries[i];
                Handles.color = i == picked ? PickedColor : entry.anchored ? AnchoredColor : StandingColor;
                Handles.DrawWireCube(entry.center, entry.halfSize * 2f);
                Handles.Label(entry.center, names[i]);
            }

            Handles.matrix = oldMatrix;
            Handles.zTest = oldTest;
        }

        private void DrawHolders(SectionEntry[] entries, int index)
        {
            var holders = SectionShape.Bits(entries, entries[index].supports);
            Handles.color = index == picked ? PickedColor : new Color(1f, 1f, 1f, 0.35f);
            for (var holder = 0; holder < entries.Length; holder++)
            {
                if ((holders & (1ul << holder)) != 0)
                {
                    Handles.DrawLine(entries[index].center, entries[holder].center);
                }
            }
        }
    }
}
