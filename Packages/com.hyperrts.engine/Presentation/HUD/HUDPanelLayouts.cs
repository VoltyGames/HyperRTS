using HyperRTS.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace HyperRTS.Presentation.HUD
{
    /// <summary>The UXML shells and row templates of the engine's HUD panels, handed to each panel by the HUD.</summary>
    [CreateAssetMenu(menuName = HyperRTSMenu.Rig + "HUD Panel Layouts", fileName = "HUDPanelLayouts")]
    public sealed class HUDPanelLayouts : ScriptableObject
    {
        [SerializeField]
        [Tooltip("Top bar shell (ResourceBar.uxml).")]
        private VisualTreeAsset resourceBar;

        [SerializeField]
        [Tooltip("One stockpile entry in the top bar (ResourceBarEntry.uxml).")]
        private VisualTreeAsset resourceBarEntry;

        [SerializeField]
        [Tooltip("Selection panel shell (SelectionPanel.uxml).")]
        private VisualTreeAsset selectionPanel;

        [SerializeField]
        [Tooltip("One entity type tile in a group selection (SelectionPanelGroup.uxml).")]
        private VisualTreeAsset selectionPanelGroup;

        [SerializeField]
        [Tooltip("One production queue slot (ProductionQueueViewSlot.uxml).")]
        private VisualTreeAsset productionQueueViewSlot;

        [SerializeField]
        [Tooltip("Command card shell (CommandCard.uxml).")]
        private VisualTreeAsset commandCard;

        [SerializeField]
        [Tooltip("Victory and defeat banner (GameOverBanner.uxml).")]
        private VisualTreeAsset gameOverBanner;

        [SerializeField]
        [Tooltip("Minimap shell with its fog, blip and view layers (Minimap.uxml).")]
        private VisualTreeAsset minimap;

        public VisualTreeAsset ResourceBar => resourceBar;

        public VisualTreeAsset ResourceBarEntry => resourceBarEntry;

        public VisualTreeAsset SelectionPanel => selectionPanel;

        public VisualTreeAsset SelectionPanelGroup => selectionPanelGroup;

        public VisualTreeAsset ProductionQueueViewSlot => productionQueueViewSlot;

        public VisualTreeAsset CommandCard => commandCard;

        public VisualTreeAsset GameOverBanner => gameOverBanner;

        public VisualTreeAsset Minimap => minimap;
    }
}
