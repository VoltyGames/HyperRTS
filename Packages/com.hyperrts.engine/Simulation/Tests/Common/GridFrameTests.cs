using System.Collections.Generic;
using HyperRTS.Simulation.Common;
using NUnit.Framework;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Tests.Common
{
    /// <summary>Grid edge cases: rays along a cell border and through corners, and cell sets at the grid's edge.</summary>
    public class GridFrameTests
    {
        private static readonly GridFrame Frame = new(new float2(-4f, -4f), 2f, new int2(4, 4));

        private static List<(int2 Cell, float Entry, float Exit, bool Corner)> Walk(float2 from, float2 to)
        {
            var crossed = new List<(int2, float, float, bool)>();
            var ray = new GridRay(Frame, from, to);
            while (ray.MoveNext())
            {
                crossed.Add((ray.Cell, ray.Entry, ray.Exit, ray.CrossedCorner));
            }

            return crossed;
        }

        [Test]
        public void Ray_AlongACellBorder_StaysInTheRowAbove()
        {
            var crossed = Walk(new float2(-3f, 0f), new float2(3f, 0f));

            Assert.AreEqual(4, crossed.Count);
            for (var i = 0; i < crossed.Count; i++)
            {
                Assert.AreEqual(new int2(i, 2), crossed[i].Cell, "a border belongs to the cell above it");
            }

            Assert.AreEqual(0f, crossed[0].Entry);
            Assert.AreEqual(1f / 6f, crossed[0].Exit, 1e-6f);
            Assert.AreEqual(1f, crossed[3].Exit);
        }

        [Test]
        public void Ray_ThroughCorners_StepsDiagonallyAndCoversTheWholeLine()
        {
            var crossed = Walk(new float2(-3f, -3f), new float2(3f, 3f));

            Assert.AreEqual(4, crossed.Count, "no cell beside a corner is crossed");
            Assert.IsFalse(crossed[0].Corner);
            var length = 0f;
            for (var i = 0; i < crossed.Count; i++)
            {
                Assert.AreEqual(new int2(i, i), crossed[i].Cell);
                Assert.AreEqual(i > 0, crossed[i].Corner);
                length += crossed[i].Exit - crossed[i].Entry;
            }

            Assert.AreEqual(1f, length, 1e-6f, "the crossings add up to the line");
        }

        [Test]
        public void Cells_OffTheGridEdge_AreDropped()
        {
            var circle = new List<int2>();
            foreach (var cell in Frame.CellsInCircle(new float2(-4f, -4f), 2.5f))
            {
                circle.Add(cell);
            }

            CollectionAssert.AreEqual(new[] { new int2(0, 0) }, circle, "only centres within the radius count");

            var offsets = new List<int2>();
            foreach (var offset in new Neighbours())
            {
                offsets.Add(offset);
            }

            Assert.AreEqual(8, offsets.Count);
            Assert.AreEqual(new int2(-1, -1), offsets[0]);
            CollectionAssert.DoesNotContain(offsets, int2.zero);
        }
    }
}
