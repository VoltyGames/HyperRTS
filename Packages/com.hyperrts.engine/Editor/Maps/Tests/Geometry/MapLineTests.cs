using System.Collections.Generic;
using HyperRTS.Editor.Maps.Geometry;
using NUnit.Framework;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Tests.Geometry
{
    /// <summary>A line's cell-sorted distance lookup gives the same distance and along as walking every segment.</summary>
    public class MapLineTests
    {
        [Test]
        public void Distance_MatchesEverySegmentWalk_OnRandomLatticeLines()
        {
            var random = new System.Random(7);
            for (var trial = 0; trial < 200; trial++)
            {
                // Lattice points and queries make equal distances common, so ties are tested too.
                var spacing = trial % 3 == 0 ? 400f : 1f + trial % 5;
                var line = new MapLine(LatticeLine(random, 1 + trial % 40, spacing), 1f, spacing * 3f);
                for (var query = 0; query < 200; query++)
                {
                    var point = new Vector2(random.Next(-6, 26), random.Next(-6, 26)) * (spacing * 0.5f);
                    if (!line.Bounds.Contains(point))
                    {
                        continue;
                    }

                    var expected = MapGeometry.DistanceToPolyline(line.Points, point, out var expectedAlong);
                    Assert.AreEqual(expected, line.Distance(point, out var along), $"distance at {point}, trial {trial}");
                    Assert.AreEqual(expectedAlong, along, $"along at {point}, trial {trial}");
                }
            }
        }

        [Test]
        public void Distance_OnALineThatDoublesBack_TakesTheFirstPass()
        {
            var line = new MapLine(new List<Vector2> { new(0f, 0f), new(10f, 0f), new(0f, 0f) }, 1f, 5f);

            Assert.AreEqual(3f, line.Distance(new Vector2(5f, 3f), out var along));
            Assert.AreEqual(5f, along);
        }

        private static List<Vector2> LatticeLine(System.Random random, int count, float spacing)
        {
            var points = new List<Vector2>();
            for (var i = 0; i < count; i++)
            {
                points.Add(new Vector2(random.Next(0, 10), random.Next(0, 10)) * spacing);
            }

            return points;
        }
    }
}
