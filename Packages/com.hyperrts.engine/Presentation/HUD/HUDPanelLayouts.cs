using HyperRTS.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace HyperRTS.Presentation.HUD
{
    /// <summary>The HUD tree (its panel shells are slots in it) and the row templates the HUD's panels clone.</summary>
    [CreateAssetMenu(menuName = HyperRTSMenu.Rig + "HUD Panel Layouts", fileName = "HUDPanelLayouts")]
    public sealed class HUDPanelLayouts : ScriptableObject
    {
        [SerializeField]
        [Tooltip("HUD tree (HUD.uxml): hud-root with a template instance per panel shell, in the order panels stack.")]
        private VisualTreeAsset root;

        [SerializeField]
        [Tooltip("One stockpile entry in the top bar (ResourceBarEntry.uxml).")]
        private VisualTreeAsset resourceBarEntry;

        [SerializeField]
        [Tooltip("One entity type tile in a group selection (SelectionPanelGroup.uxml).")]
        private VisualTreeAsset selectionPanelGroup;

        [SerializeField]
        [Tooltip("One production queue slot (ProductionQueueViewSlot.uxml).")]
        private VisualTreeAsset productionQueueViewSlot;

        [SerializeField]
        [Tooltip("Sweep, badges and cost strip of a build card (CommandButtonDetails.uxml).")]
        private VisualTreeAsset commandButtonDetails;

        [SerializeField]
        [Tooltip("Drag-box marquee and its corner accents (SelectionDragBoxUI.uxml).")]
        private VisualTreeAsset selectionDragBoxUI;

        public VisualTreeAsset Root => root;

        public VisualTreeAsset ResourceBarEntry => resourceBarEntry;

        public VisualTreeAsset SelectionPanelGroup => selectionPanelGroup;

        public VisualTreeAsset ProductionQueueViewSlot => productionQueueViewSlot;

        public VisualTreeAsset CommandButtonDetails => commandButtonDetails;

        public VisualTreeAsset SelectionDragBoxUI => selectionDragBoxUI;
    }
}
