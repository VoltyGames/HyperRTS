using HyperRTS.Editor.Authoring;
using NUnit.Framework;

namespace HyperRTS.Editor.Tests
{
    /// <summary>A dragged box face mirrors onto the opposite side, so centred boxes grow and shrink alike.</summary>
    public class GroundHandlesTests
    {
        // The opposite face stays put, so moving the max (+1) or min (-1) face by d shifts the centre by d / 2.
        [TestCase(1f, 1f, 6f, TestName = "MaxFaceOutwardGrows")]
        [TestCase(1f, -1f, 2f, TestName = "MaxFaceInwardShrinks")]
        [TestCase(-1f, 1f, 6f, TestName = "MinFaceOutwardGrows")]
        [TestCase(-1f, -1f, 2f, TestName = "MinFaceInwardShrinks")]
        [TestCase(1f, 0f, 4f, TestName = "UntouchedBoxKeepsItsSize")]
        public void DraggedFaceMirrors(float side, float drag, float expected) =>
            Assert.AreEqual(expected, GroundHandles.MirroredSize(4f, side * drag * 0.5f, 4f + drag), 1e-4f);

        [Test]
        public void DirectionLiesFlatAtTheGivenAngle()
        {
            var direction = GroundHandles.Direction(90f);
            Assert.AreEqual(0f, direction.x, 1e-4f);
            Assert.AreEqual(0f, direction.y, 1e-4f);
            Assert.AreEqual(1f, direction.z, 1e-4f);
        }
    }
}
