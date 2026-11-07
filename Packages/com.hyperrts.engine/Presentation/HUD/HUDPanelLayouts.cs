using HyperRTS.Presentation.HUD.Panels;
using HyperRTS.Simulation.Common.Authoring;
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
        [Tooltip("Every part of a command card button (CommandButton.uxml).")]
        private VisualTreeAsset commandButton;

        [SerializeField]
        [Tooltip("Drag-box marquee and its corner accents (SelectionDragBoxUI.uxml).")]
        private VisualTreeAsset selectionDragBoxUI;

        [SerializeField]
        [Tooltip("One soldier of a selected squad (SquadMemberCard.uxml).")]
        private VisualTreeAsset squadMemberCard;

        [SerializeField]
        [Tooltip("One strategic asset countdown line (StrategicCountdownRow.uxml).")]
        private VisualTreeAsset strategicCountdownRow;

        [SerializeField]
        [Tooltip("One name and value line in a tooltip (HUDTooltipStat.uxml).")]
        private VisualTreeAsset tooltipStat;

        [SerializeField]
        [Tooltip("An empty command card slot (CommandBlank.uxml).")]
        private VisualTreeAsset commandBlank;

        public VisualTreeAsset Root => root;

        public VisualTreeAsset ResourceBarEntry => resourceBarEntry;

        public VisualTreeAsset SelectionPanelGroup => selectionPanelGroup;

        public VisualTreeAsset ProductionQueueViewSlot => productionQueueViewSlot;

        public VisualTreeAsset CommandButton => commandButton;

        public VisualTreeAsset SelectionDragBoxUI => selectionDragBoxUI;

        public VisualTreeAsset SquadMemberCard => squadMemberCard;

        public VisualTreeAsset StrategicCountdownRow => strategicCountdownRow;

        public VisualTreeAsset TooltipStat => tooltipStat;

        public VisualTreeAsset CommandBlank => commandBlank;
    }
}
