using HyperRTS.Editor.Maps.Checks;
using HyperRTS.Editor.Maps.Heights;
using HyperRTS.Editor.Maps.Land;
using HyperRTS.Editor.Maps.Report;
using NUnit.Framework;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Tests
{
    /// <summary>A frame counts once it holds the rules' minimum kinds of interest.</summary>
    public class MapCompositionTests
    {
        [Test]
        public void Measure_CountsFramesHoldingEnoughKinds()
        {
            var bounds = new Rect(-60f, -40f, 120f, 80f);
            var land = new MapLand(bounds);
            var wood = land.Add(new MapLandRegion(MapLandKind.WoodEdge, "Wood"));
            var water = land.Add(new MapLandRegion(MapLandKind.Water, "Water"));
            var field = land.Add(new MapLandRegion(MapLandKind.Field, "Field"));
            // ClaimWhere also visits the cell past each edge, so the water stops short of the next frame.
            var cornerFrame = new Rect(-60f, -40f, 60f, 40f);
            land.ClaimWhere(cornerFrame, wood, p => p.x < -30f && p.y < -20f);
            land.ClaimWhere(cornerFrame, water, p => p.x < -30f && p.y >= -20f && p.y < 0f);
            land.ClaimWhere(bounds, field, _ => true);
            var heights = new MapHeights(121, bounds);
            var rules = MapFixture.Item<MapRules>("Rules");

            var composition = MapComposition.Measure(land, heights, bounds, rules);

            Assert.AreEqual(4, composition.frames);
            Assert.AreEqual(0.25f, composition.share, 0.001f, "only the corner frame holds wood, water and fields");
            Assert.AreEqual(1f, composition.fieldPatternShare, 0.001f);
            Assert.AreEqual(0.25f, composition.waterShare, 0.001f);
            Assert.AreEqual(0f, composition.reliefShare, "flat ground has no relief");
        }
    }
}
