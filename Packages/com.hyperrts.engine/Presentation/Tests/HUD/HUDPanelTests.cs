using HyperRTS.Presentation.HUD;
using HyperRTS.Simulation.Navigation;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace HyperRTS.Presentation.Tests.HUD
{
    /// <summary>Panel framing, pointer rules, the UXML names the panels look up, and the minimap's view maths.</summary>
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
        public void LineCost_IsAPassThroughRowInTheHUDTree()
        {
            var lineCost = new LineCost(Layouts.Root.CloneRoot().Require<CostRow>("line-cost"));

            Assert.IsFalse(lineCost.BlocksPointer);
            Assert.IsTrue(lineCost.NeedsPlayer);
        }

        [Test]
        public void PlacementHint_IsAHiddenPassThroughLabelWithTheRotateText()
        {
            var label = Layouts.Root.CloneRoot().Require<Label>("placement-hint");
            var hint = new PlacementHint(label);

            Assert.IsFalse(hint.BlocksPointer);
            Assert.AreEqual(HUDText.Text("hud.rotate-hint"), label.text);
            Assert.IsTrue(label.ClassListContains("hrts-hidden"), "hidden until a building is placed");
        }

        [Test]
        public void Templates_HaveTheNamedElements()
        {
            var layouts = Layouts;

            var hud = layouts.Root.CloneRoot();
            Assert.IsTrue(hud.ClassListContains("hud-root"));
            hud.Require<VisualElement>("bottom");

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
        public void Minimap_RoundTripsAndPutsNorthAtTop()
        {
            var min = new float2(-100f, -50f);
            var size = new float2(200f, 100f);

            var topLeft = MinimapMath.WorldToMinimap(new float2(-100f, 50f), min, size);
            var back = MinimapMath.MinimapToWorld(new Vector2(0.25f, 0.75f), min, size);

            Assert.AreEqual(Vector2.zero, topLeft);
            Assert.AreEqual(new Vector2(0.25f, 0.75f), MinimapMath.WorldToMinimap(back, min, size));
        }

        [Test]
        public void ViewGround_HitsGroundOrCapsAboveHorizon_AndFocusFallsBackBelowTheCamera()
        {
            var eye = new float3(0f, 10f, 0f);
            var down = ViewGround.Reach(default, eye, math.normalize(new float3(0f, -1f, 1f)), 1000f);
            var up = ViewGround.Reach(default, eye, new float3(0f, 0f, 1f), 50f);
            var focus = ViewGround.Focus(default, new float3(4f, 10f, -2f), new float3(0f, 0f, 1f), 1000f);

            Assert.That(math.distance(new float3(0f, 0f, 10f), down), Is.LessThan(1e-3f));
            Assert.That(math.distance(new float3(0f, 0f, 50f), up), Is.LessThan(1e-3f));
            Assert.That(math.distance(new float3(4f, 0f, -2f), focus), Is.LessThan(1e-3f));
        }
    }
}
