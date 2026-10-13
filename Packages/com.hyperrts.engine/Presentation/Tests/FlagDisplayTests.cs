using HyperRTS.Presentation.Flags;
using NUnit.Framework;

namespace HyperRTS.Presentation.Tests
{
    public class FlagDisplayTests
    {
        [TestCase(0, 0, 0f, 0, 0f, TestName = "Neutral idle: no flag")]
        [TestCase(1, 0, 0f, 1, 1f, TestName = "Owned idle: owner at the top")]
        [TestCase(0, 2, 0.3f, 2, 0.3f, TestName = "Neutral being captured: capturer rises over the whole capture")]
        [TestCase(1, 2, 0.25f, 1, 0.5f, TestName = "Owned, first half: owner lowering")]
        [TestCase(1, 2, 0.75f, 2, 0.5f, TestName = "Owned, second half: capturer rising")]
        [TestCase(1, 2, 1f, 2, 1f, TestName = "Capture complete: capturer at the top")]
        [TestCase(1, 1, 0.5f, 1, 1f, TestName = "Owner's own progress: owner stays up")]
        public void Resolve(int owner, int capturer, float progress, int shown, float hoist)
        {
            var faction = FlagDisplay.Resolve((byte)owner, (byte)capturer, progress, out var height);

            Assert.AreEqual(shown, faction);
            Assert.AreEqual(hoist, height, 1e-5f);
        }
    }
}
