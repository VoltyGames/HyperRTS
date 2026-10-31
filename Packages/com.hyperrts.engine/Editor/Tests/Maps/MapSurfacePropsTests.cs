using System.Collections.Generic;
using HyperRTS.Editor.Maps.Data;
using HyperRTS.Editor.Maps.Land;
using HyperRTS.Editor.Maps.Placement;
using HyperRTS.Editor.Maps.Report;
using NUnit.Framework;
using UnityEngine;

namespace HyperRTS.Editor.Tests.Maps
{
    /// <summary>Props per hectare are counted by the surface each prop stands on.</summary>
    public class MapSurfacePropsTests
    {
        [Test]
        public void Measure_CountsPropsPerHectareOfTheSurfaceTheyStandOn()
        {
            var land = new MapLand(new Rect(-100f, -100f, 200f, 200f));
            var field = land.Add(new MapLandRegion(MapLandKind.Field, "Stubble"));
            var meadow = land.Add(new MapLandRegion(MapLandKind.Meadow, "Meadow"));
            land.ClaimWhere(land.Bounds, field, p => p.x < 0f);
            land.ClaimWhere(land.Bounds, meadow, p => p.x >= 0f);
            var bale = MapFixture.Item<MapProp>("HayBale");
            var placements = new List<MapPlacement>();
            for (var i = 0; i < 6; i++)
            {
                placements.Add(new MapPlacement(bale, new Vector2(-50f, -90f + 30f * i), 0f, 1f));
            }

            placements.Add(new MapPlacement(bale, new Vector2(500f, 0f), 0f, 1f));
            var measured = MapSurfaceProps.Measure(land, placements, land.Bounds);

            var stubble = measured.Find(m => m.surface == "Stubble");
            Assert.AreEqual(2f, stubble.hectares, 0.01f);
            Assert.AreEqual(3f, stubble.perHectare, 0.01f, "six bales on two hectares; the one off the map isn't counted");
            Assert.AreEqual(0f, measured.Find(m => m.surface == "Meadow").perHectare);
        }
    }
}
