using HyperRTS.Presentation.HUD;
using NUnit.Framework;

namespace HyperRTS.Presentation.Tests
{
    /// <summary>Panel framing and pointer rules the HUD controller relies on.</summary>
    public class HUDPanelTests
    {
        [Test]
        public void BoxedPanel_HasPanelClass()
        {
            var panel = new SelectionPanel();

            Assert.IsTrue(panel.Root.ClassListContains("hud-panel"));
            Assert.IsTrue(panel.BlocksPointer);
            Assert.IsTrue(panel.NeedsPlayer);
        }

        [Test]
        public void GameOverBanner_IsUnboxedPassThroughOverlay()
        {
            var banner = new GameOverBanner();

            Assert.IsTrue(banner.Root.ClassListContains("hud-banner"));
            Assert.IsFalse(banner.Root.ClassListContains("hud-panel"));
            Assert.IsFalse(banner.BlocksPointer);
            Assert.IsFalse(banner.NeedsPlayer);
        }
    }
}
