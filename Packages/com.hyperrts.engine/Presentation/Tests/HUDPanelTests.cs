using HyperRTS.Presentation.HUD;
using NUnit.Framework;
using UnityEditor;
using UnityEngine.UIElements;

namespace HyperRTS.Presentation.Tests
{
    /// <summary>Panel framing, pointer rules and the UXML names the panels look up.</summary>
    public class HUDPanelTests
    {
        private const string LayoutsGuid = "063ced5c8bae4ff58d48995eac9c6741";

        /// <summary>The engine's shipped panel templates.</summary>
        internal static HUDPanelLayouts Layouts =>
            AssetDatabase.LoadAssetAtPath<HUDPanelLayouts>(AssetDatabase.GUIDToAssetPath(LayoutsGuid));

        [Test]
        public void BoxedPanel_HasPanelClass()
        {
            var panel = new SelectionPanel(Layouts);

            Assert.IsTrue(panel.Root.ClassListContains("hud-panel"));
            Assert.IsTrue(panel.BlocksPointer);
            Assert.IsTrue(panel.NeedsPlayer);
        }

        [Test]
        public void GameOverBanner_IsUnboxedPassThroughOverlay()
        {
            var banner = new GameOverBanner(Layouts);

            Assert.IsTrue(banner.Root.ClassListContains("hud-banner"));
            Assert.IsFalse(banner.Root.ClassListContains("hud-panel"));
            Assert.IsFalse(banner.BlocksPointer);
            Assert.IsFalse(banner.NeedsPlayer);
        }

        // Each constructor requires every element it names, so building them checks the shells.
        [Test]
        public void EveryPanel_BuildsFromItsShell()
        {
            var layouts = Layouts;

            Assert.DoesNotThrow(() => new ResourceBar(layouts));
            Assert.DoesNotThrow(() => new Minimap(layouts));
            Assert.DoesNotThrow(() => new CommandCard(layouts));
        }

        [Test]
        public void RowTemplates_HaveTheNamedElements()
        {
            var layouts = Layouts;

            var entry = layouts.ResourceBarEntry.CloneRoot();
            entry.Require<HUDIcon>("icon");
            entry.Require<Label>("name");
            entry.Require<Label>("amount");

            var group = layouts.SelectionPanelGroup.CloneRoot();
            group.Require<HUDIcon>("icon");
            group.Require<Label>("count");

            var slot = layouts.ProductionQueueViewSlot.CloneRoot();
            Assert.IsInstanceOf<Button>(slot);
            slot.Require<HUDIcon>("icon");
            slot.Require<HUDBar>("progress");
        }

        [Test]
        public void HUDLayout_HasBottomSlot()
        {
            var hud = Layouts.Root.CloneRoot();

            Assert.IsTrue(hud.ClassListContains("hud-root"));
            hud.Require<VisualElement>("bottom");
        }
    }
}
