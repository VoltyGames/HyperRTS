using HyperRTS.Simulation.Navigation;
using NUnit.Framework;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Tests
{
    /// <summary>Turned obstacle and area boxes: their world bounds and which points they cover.</summary>
    public class NavBoxTests
    {
        [Test]
        public void QuarterTurn_SwapsWidthAndDepth_AndStaysAxisAligned()
        {
            var box = new NavBox(float3.zero, quaternion.RotateY(math.PI * 0.5f), new float2(10f, 4f));

            Assert.IsTrue(box.IsAxisAligned);
            Assert.AreEqual(2f, box.Extents.x, 1e-4f);
            Assert.AreEqual(5f, box.Extents.y, 1e-4f);
        }

        [Test]
        public void DiagonalBox_CoversItsCentreButNotTheCornersOfItsBounds()
        {
            var box = new NavBox(float3.zero, quaternion.RotateY(math.PI * 0.25f), new float2(10f, 2f));
            var corner = box.Extents * 0.95f;

            Assert.IsFalse(box.IsAxisAligned);
            Assert.IsTrue(box.Contains(float2.zero, 0f));
            Assert.IsFalse(box.Contains(new float2(corner.x, -corner.y), 0f), "the empty corner of the world box");
            Assert.IsTrue(box.Contains(new float2(3f, -3f), 0f) || box.Contains(new float2(3f, 3f), 0f), "along its length");
        }
    }
}
