using HyperRTS.Editor.Authoring;
using NUnit.Framework;

namespace HyperRTS.Editor.Tests
{
    /// <summary>A dragged box face mirrors onto the opposite side, so centred boxes grow and shrink alike.</summary>
    public class GroundHandlesTests
    {
        // The handle keeps the opposite face, so moving one face by d shifts its centre by d / 2.
        [TestCase(1f, 6f, TestName = "MaxFaceOutwardGrows")]
        [TestCase(-1f, 2f, TestName = "MaxFaceInwardShrinks")]
        public void MaxFaceMoves(float drag, float expected) =>
            Assert.AreEqual(expected, GroundHandles.MirroredSize(4f, drag * 0.5f, 4f + drag), 1e-4f);

        [TestCase(1f, 6f, TestName = "MinFaceOutwardGrows")]
        [TestCase(-1f, 2f, TestName = "MinFaceInwardShrinks")]
        public void MinFaceMoves(float drag, float expected) =>
            Assert.AreEqual(expected, GroundHandles.MirroredSize(4f, -drag * 0.5f, 4f + drag), 1e-4f);

        [Test]
        public void UntouchedBoxKeepsItsSize() => Assert.AreEqual(4f, GroundHandles.MirroredSize(4f, 0f, 4f), 1e-4f);
    }
}
