using System.Collections.Generic;
using HyperRTS.Editor.Maps;
using HyperRTS.Editor.Maps.Planning.Farmland;
using NUnit.Framework;
using UnityEngine;

namespace HyperRTS.Editor.Tests
{
    /// <summary>Field-end tracks: carried onto a road within reach, dropped when they lead nowhere.</summary>
    public class MapFieldTrackLinksTests
    {
        /// <summary>Links over the road fixture with one asphalt road (8 m) along z = -60.</summary>
        private static MapFieldTrackLinks Links()
        {
            var spec = MapRoadFixture.Spec();
            spec.roads.Add(MapRoadFixture.Road("Asphalt", new Vector2(-110f, -60f), new Vector2(110f, -60f)));
            return new MapFieldTrackLinks(MapRoadFixture.Context(spec, MapRoadFixture.Climate(), new MapIssues()));
        }

        [Test]
        public void ARunEndingBesideARoad_IsCarriedOntoIt()
        {
            var run = new List<Vector2> { new(40f, -67f), new(40f, -95f) };

            Assert.IsTrue(Links().Link(run));

            Assert.AreEqual(3, run.Count, "the road point is added before the start");
            Assert.AreEqual(-60f, run[0].y, 0.6f, "on the road's centre line");
            Assert.AreEqual(40f, run[0].x, 1.5f);
            Assert.AreEqual(new Vector2(40f, -95f), run[^1], "the field end stays");
        }

        [Test]
        public void ARunThatReachesNoRoad_IsDropped()
        {
            var run = new List<Vector2> { new(60f, -80f), new(60f, -100f) };

            Assert.IsFalse(Links().Link(run), "16 m off the road edge is out of reach");
            Assert.AreEqual(2, run.Count, "nothing snapped");
        }

        [Test]
        public void ARunToTheMapEdge_LeadsSomewhere()
        {
            var run = new List<Vector2> { new(-80f, -95f), new(-120f, -95f) };

            Assert.IsTrue(Links().Link(run));
            Assert.AreEqual(2, run.Count);
        }
    }
}
