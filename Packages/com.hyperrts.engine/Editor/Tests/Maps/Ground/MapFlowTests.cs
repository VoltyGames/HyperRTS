using HyperRTS.Editor.Maps.Ground;
using NUnit.Framework;
using UnityEngine;

namespace HyperRTS.Editor.Tests.Maps.Ground
{
    /// <summary>Drainage: valley floors gather the slopes' runoff, and every symmetry copy drains alike.</summary>
    public class MapFlowTests
    {
        [Test]
        public void ValleyFloor_Drains_WhileItsSlopesStayDry()
        {
            var layout = MapFixture.Layout(MapFixture.Spec());
            // A valley along z, falling to the south.
            var flow = new MapFlow(layout.Symmetry, Heights(layout, p => 0.08f * Mathf.Abs(p.x) + 0.02f * p.y));

            Assert.Greater(flow.At(new Vector2(0f, -60f)), 0.8f, "the valley floor downstream");
            Assert.Less(flow.At(new Vector2(40f, -60f)), 0.1f, "its slope");
            for (var x = -100f; x < 100f; x += 13.7f)
            {
                var p = new Vector2(x, 0.6f * x - 20f);
                Assert.AreEqual(flow.At(p), flow.At(-p), 1e-4f, $"drainage at {p} and its copy");
            }
        }

        private static MapHeights Heights(Editor.Maps.Layout.MapLayout layout, System.Func<Vector2, float> height)
        {
            var heights = new MapHeights(257, layout.TerrainBounds);
            for (var z = 0; z < heights.Resolution; z++)
            {
                for (var x = 0; x < heights.Resolution; x++)
                {
                    heights[x, z] = height(heights.Position(x, z));
                }
            }

            return heights;
        }
    }
}
