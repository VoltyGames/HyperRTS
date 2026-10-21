using HyperRTS.Editor.Maps;
using HyperRTS.Editor.Maps.Data;
using HyperRTS.Editor.Maps.Geometry;
using HyperRTS.Editor.Maps.Placement;
using HyperRTS.Editor.Maps.Spec;
using NUnit.Framework;
using UnityEngine;

namespace HyperRTS.Editor.Tests
{
    /// <summary>Spec props that stand on their own pad must not straddle a road.</summary>
    public class MapPropRoadTests
    {
        [Test]
        public void APaddedPropOnARoad_IsAnErrorNamingIt()
        {
            var issues = Place(new Vector2(1f, -40f));
            Assert.IsTrue(issues.HasErrors);
            StringAssert.Contains("'Pumpjack' at (1, -40) stands on a road", issues.ToString());
        }

        [Test]
        public void APaddedPropBesideTheRoad_IsFine()
        {
            var issues = Place(new Vector2(12f, -40f));
            Assert.IsFalse(issues.HasErrors, issues.ToString());
        }

        private static MapIssues Place(Vector2 position)
        {
            var spec = MapRoadFixture.Spec();
            spec.roads.Add(MapRoadFixture.Road("Dirt", new Vector2(0f, -120f), new Vector2(0f, 0f)));
            spec.props.Add(new MapPropSpec { pos = new MapPoint(position.x, position.y), kind = "Pumpjack" });
            var climate = MapRoadFixture.Climate();
            var layout = MapRoadFixture.Layout(spec);
            var palette = new MapPalette();
            palette.baseRadius = 15f;
            palette.entries.Add(new MapProp { key = "Pumpjack", size = new Vector2(8f, 16f), pad = "Dirt" });
            var shapes = new MapShapes(layout, palette, climate);
            var issues = new MapIssues();
            MapPropPlacer.Place(layout, shapes, palette, climate, issues);
            return issues;
        }
    }
}
