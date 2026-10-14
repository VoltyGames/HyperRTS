using System.Linq;
using HyperRTS.Editor.Maps;
using HyperRTS.Editor.Maps.Checks;
using HyperRTS.Editor.Maps.Data;
using HyperRTS.Editor.Maps.Ground;
using HyperRTS.Editor.Maps.Land;
using HyperRTS.Simulation.Effects;
using NUnit.Framework;
using UnityEngine;

namespace HyperRTS.Editor.Tests
{
    public class MapGroundMaterialTests
    {
        private const int Size = 20;

        [Test]
        public void Painter_WritesSurfaceRoadAndWaterClasses()
        {
            var climate = MapRoadFixture.Climate();
            climate.Surface("Dirt").impactClass = "Soil";
            var shapes = MapRoadFixture.Shapes(MapRoadFixture.Layout(MapRoadFixture.Spec()), climate);
            shapes.AddRoad(MapRoadFixture.RoadShape(climate.roadClasses[0], false, new Vector2(-10f, 0f), new Vector2(10f, 0f)));
            var land = new MapLand(new Rect(-Size * 0.5f, -Size * 0.5f, Size, Size));
            Claim(land, new MapLandRegion(MapLandKind.Water, "Meadow"), p => p.y < -5f);
            Claim(land, new MapLandRegion(MapLandKind.Road, "Asphalt"), p => Mathf.Abs(p.y) < 2f);
            Claim(land, new MapLandRegion(MapLandKind.Field, "Dirt"), p => p.y > 5f);

            var cells = MapGroundMaterialPainter.Paint(land, shapes, climate);

            Assert.AreEqual(SurfaceMaterial.Water, At(cells, 2), "river bed");
            Assert.AreEqual(SurfaceMaterial.Grass, At(cells, 7), "unclaimed ground takes the ground surface");
            Assert.AreEqual(SurfaceMaterial.Paved, At(cells, 10), "road takes its class, not its Grass surface");
            Assert.AreEqual(SurfaceMaterial.Soil, At(cells, 17), "field takes its surface");
        }

        [TestCase(null, TestName = "AMissingSurfaceImpactClass_IsAnErrorNamingIt")]
        [TestCase("None", TestName = "ANoneSurfaceImpactClass_IsAnErrorNamingIt")]
        [TestCase("Lava", TestName = "AnUnknownSurfaceImpactClass_IsAnErrorNamingIt")]
        public void SurfaceImpactClass_IsChecked(string value)
        {
            var climate = MapRoadFixture.Climate();
            climate.Surface("Verge").impactClass = value;

            Assert.IsTrue(ImpactErrors(climate).Any(e => e.Contains("surface 'Verge'")), "no error naming the surface");
        }

        [TestCase(null, TestName = "AMissingRoadImpactClass_IsAnErrorNamingIt")]
        [TestCase("Tarmac", TestName = "AnUnknownRoadImpactClass_IsAnErrorNamingIt")]
        public void RoadImpactClass_IsChecked(string value)
        {
            var climate = MapRoadFixture.Climate();
            climate.roadClasses[1].impactClass = value;

            Assert.IsTrue(ImpactErrors(climate).Any(e => e.Contains("road class 'Dirt'")), "no error naming the road class");
        }

        [Test]
        public void ValidImpactClasses_PassTheCheck() =>
            CollectionAssert.IsEmpty(ImpactErrors(MapRoadFixture.Climate()));

        private static string[] ImpactErrors(MapClimate climate)
        {
            var issues = new MapIssues();
            MapAssetCheck.Check(climate, new MapPalette(), issues);
            return issues.Errors.Where(e => e.Contains("impactClass")).ToArray();
        }

        private static void Claim(MapLand land, MapLandRegion region, System.Func<Vector2, bool> inside) =>
            land.ClaimWhere(land.Bounds, land.Add(region), inside);

        /// <summary>The cell in the middle column of row <paramref name="z"/>.</summary>
        private static SurfaceMaterial At(byte[] cells, int z) => (SurfaceMaterial)cells[z * Size + Size / 2];
    }
}
