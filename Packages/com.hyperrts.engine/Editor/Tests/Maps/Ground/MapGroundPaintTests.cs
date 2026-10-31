using System;
using System.Collections.Generic;
using System.Linq;
using HyperRTS.Editor.Maps;
using HyperRTS.Editor.Maps.Checks;
using HyperRTS.Editor.Maps.Climate;
using HyperRTS.Editor.Maps.Geometry;
using HyperRTS.Editor.Maps.Ground;
using HyperRTS.Editor.Maps.Land;
using HyperRTS.Editor.Maps.Layout;
using HyperRTS.Editor.Maps.Palette;
using HyperRTS.Presentation.Environment.RowCrops;
using HyperRTS.Simulation.Cues;
using NUnit.Framework;
using UnityEngine;

namespace HyperRTS.Editor.Tests.Maps.Ground
{
    /// <summary>Per-cell ground maps painted from the land: impact materials, row directions and row crop fields.</summary>
    public class MapGroundPaintTests : TemplateFixture
    {
        private const int MaterialSize = 20;
        private const float RowAngle = 30f;

        [Test]
        public void MaterialPainter_WritesSurfaceRoadAndWaterClasses()
        {
            var climate = MapFixture.Climate();
            climate.Surface("Dirt").impactClass = "Soil";
            var land = new MapLand(new Rect(-MaterialSize * 0.5f, -MaterialSize * 0.5f, MaterialSize, MaterialSize));
            Claim(land, new MapLandRegion(MapLandKind.Water, "Meadow"), p => p.y < -5f);
            Claim(land, MapLandRegion.Road("Asphalt", "Paved"), p => Mathf.Abs(p.y) < 2f);
            Claim(land, new MapLandRegion(MapLandKind.Field, "Dirt"), p => p.y > 5f);

            var cells = MapGroundMaterialPainter.Paint(land, climate);

            Assert.AreEqual(SurfaceMaterial.Water, MaterialAt(cells, 2), "river bed");
            Assert.AreEqual(SurfaceMaterial.Grass, MaterialAt(cells, 7), "unclaimed ground takes the ground surface");
            Assert.AreEqual(SurfaceMaterial.Paved, MaterialAt(cells, 10), "road takes its class, not its Grass surface");
            Assert.AreEqual(SurfaceMaterial.Soil, MaterialAt(cells, 17), "field takes its surface");
        }

        [Test]
        public void MaterialPainter_ARoadRegionWithoutAnImpactClass_IsAnError()
        {
            var land = new MapLand(new Rect(-MaterialSize * 0.5f, -MaterialSize * 0.5f, MaterialSize, MaterialSize));
            Claim(land, new MapLandRegion(MapLandKind.Road, "Asphalt"), p => Mathf.Abs(p.y) < 2f);

            var error = Assert.Throws<InvalidOperationException>(() => MapGroundMaterialPainter.Paint(land, MapFixture.Climate()));
            StringAssert.Contains("'Asphalt' has no impact class", error.Message);
        }

        [TestCase("Verge", null, "surface 'Verge'", TestName = "AMissingSurfaceImpactClass_IsAnErrorNamingIt")]
        [TestCase("Verge", "None", "surface 'Verge'", TestName = "ANoneSurfaceImpactClass_IsAnErrorNamingIt")]
        [TestCase("Verge", "Lava", "surface 'Verge'", TestName = "AnUnknownSurfaceImpactClass_IsAnErrorNamingIt")]
        [TestCase("Dirt", null, "road class 'Dirt'", TestName = "AMissingRoadImpactClass_IsAnErrorNamingIt")]
        [TestCase("Dirt", "Tarmac", "road class 'Dirt'", TestName = "AnUnknownRoadImpactClass_IsAnErrorNamingIt")]
        public void ABadImpactClass_IsAnErrorNamingIt(string key, string value, string named)
        {
            var climate = MapFixture.Climate();
            if (key == "Verge")
            {
                climate.Surface(key).impactClass = value;
            }
            else
            {
                climate.RoadClass(key).impactClass = value;
            }

            Assert.IsTrue(ImpactErrors(climate).Any(e => e.Contains(named)), "no error naming " + named);
        }

        [Test]
        public void ValidImpactClasses_PassTheCheck() => CollectionAssert.IsEmpty(ImpactErrors(MapFixture.Climate()));

        [TestCase(MapSymmetry.MirrorXName)]
        [TestCase(MapSymmetry.MirrorZName)]
        [TestCase("rotate-180")]
        public void RowPainter_TurnsEachFieldsRowsThroughItsCopy(string symmetryName)
        {
            var land = RowLand();
            var symmetry = MapSymmetry.Parse(symmetryName);
            var pixels = MapRowPainter.Paint(land, symmetry);
            var fields = 0;

            land.ForEach((x, z, center) =>
            {
                var pixel = pixels[z * land.Width + x];
                if (!IsField(land, x, z))
                {
                    Assert.AreEqual(MapRowPainter.NoRows, pixel, $"land without rows at ({x}, {z}) is neutral");
                    return;
                }

                symmetry.ToSector(center, out var copy);
                var expected = symmetry.Copies[copy].Apply(MapGeometry.Forward(RowAngle));
                Assert.AreEqual(expected.x * 0.5f + 0.5f, pixel.r, 1e-4f, $"cell ({x}, {z})");
                Assert.AreEqual(expected.y * 0.5f + 0.5f, pixel.g, 1e-4f, $"cell ({x}, {z})");
                fields++;
            });
            Assert.Greater(fields, 0);
        }

        [Test]
        public void RowPainter_MirroredCopy_MirrorsTheDirection()
        {
            var land = RowLand();
            var pixels = MapRowPainter.Paint(land, MapSymmetry.Parse(MapSymmetry.MirrorXName));

            var row = land.Height / 2 * land.Width;
            var (west, east) = (pixels[row + 2], pixels[row + land.Width - 3]);
            Assert.AreEqual(1f, west.r + east.r, 1e-5f, "x flips");
            Assert.AreEqual(west.g, east.g, 1e-5f, "z stays");
        }

        [Test]
        public void CropPainter_MirroredCopies_SowMirroredIdsAndPlants()
        {
            var (bake, crop, _) = PaintCrops(withRoad: false, (_, _, _) => false);

            Assert.AreEqual(2, bake.Fields.Count, "one field per copy");
            for (var z = 0; z < bake.Height; z++)
            {
                for (var x = 0; x < bake.Width; x++)
                {
                    var mirrored = bake.Id(bake.Width - 1 - x, z);
                    Assert.AreEqual(bake.Id(x, z) == 0, mirrored == 0, $"cell ({x}, {z})");
                }
            }

            var (west, east) = (bake.Fields[0], bake.Fields[1]);
            for (var row = -20; row <= 20; row += 3)
            {
                for (var plant = -40; plant <= 40; plant += 7)
                {
                    var a = RowCropSpots.Position(west, row, plant, crop);
                    var b = RowCropSpots.Position(east, row, plant, crop);
                    Assert.AreEqual(-a.x, b.x, 1e-4f);
                    Assert.AreEqual(a.y, b.y, 1e-4f);
                }
            }
        }

        [Test]
        public void CropPainter_Roads_AndBlockedSpots_StayBare()
        {
            var (bake, _, land) = PaintCrops(withRoad: true, (point, _, _) => point.x is > -20f and < -18f);

            land.ForEach((x, z, center) =>
            {
                var nearRoad = Mathf.Abs(center.y) < 2.5f;
                var blocked = center.x is > -20f and < -18f;
                if (nearRoad || blocked)
                {
                    Assert.AreEqual(RowCropFields.None, bake.Id(x, z), $"cell at {center}");
                }
            });
            Assert.Greater(bake.Plants, 0);
        }

        [Test]
        public void CropPainter_Tiles_ListEveryCropCellsField()
        {
            var (bake, _, _) = PaintCrops(withRoad: true, (_, _, _) => false);
            var tiles = new Dictionary<(int, int), RowCropTile>();
            bake.Tiles.ForEach(tile => tiles[(tile.x, tile.z)] = tile);
            for (var z = 0; z < bake.Height; z++)
            {
                for (var x = 0; x < bake.Width; x++)
                {
                    var id = bake.Id(x, z);
                    if (id == RowCropFields.None)
                    {
                        continue;
                    }

                    Assert.IsTrue(tiles.TryGetValue((x / 16, z / 16), out var tile), $"cell ({x}, {z}) has no tile");
                    CollectionAssert.Contains(bake.TileFields.GetRange(tile.firstField, tile.fieldCount), id - 1);
                }
            }
        }

        private static string[] ImpactErrors(MapClimate climate)
        {
            var issues = new MapIssues();
            MapAssetCheck.Check(climate, MapFixture.Item<MapPalette>("Palette"), issues);
            return issues.Errors.Select(e => e.Message).Where(e => e.Contains("impactClass")).ToArray();
        }

        private static void Claim(MapLand land, MapLandRegion region, Func<Vector2, bool> inside) =>
            land.ClaimWhere(land.Bounds, land.Add(region), inside);

        /// <summary>The cell in the middle column of row <paramref name="z"/>.</summary>
        private static SurfaceMaterial MaterialAt(byte[] cells, int z) =>
            (SurfaceMaterial)cells[z * MaterialSize + MaterialSize / 2];

        private static bool IsField(MapLand land, int x, int z) => land.Region(x, z)?.Kind == MapLandKind.Field;

        /// <summary>A 32 m map: a field with rows at 30 degrees on both halves and a meadow strip down the middle.</summary>
        private static MapLand RowLand()
        {
            var land = new MapLand(new Rect(-16f, -16f, 32f, 32f));
            Claim(land, new MapLandRegion(MapLandKind.Meadow, "Grass"), p => Mathf.Abs(p.x) < 3f);
            Claim(land, new MapLandRegion(MapLandKind.Field, "Sown", 7, RowAngle), p => Mathf.Abs(p.x) > 4f);
            return land;
        }

        /// <summary>Crops on a field across both halves of a 64 m map, with an optional east-west road through it.</summary>
        private (MapCropBake Bake, RowCrop Crop, MapLand Land) PaintCrops(bool withRoad, Func<Vector2, float, float, bool> blocked)
        {
            var crop = Track(ScriptableObject.CreateInstance<RowCrop>());
            var climate = MapFixture.Item<MapClimate>("Test");
            climate.surfaces.Add(MapFixture.Item<MapClimateSurface>("Sown", i => i.rowCrop = "Crop"));
            climate.surfaces.Add(MapFixture.Item<MapClimateSurface>("Track"));
            climate.rowCrops.Add(MapFixture.Item<MapClimateRowCrop>("Crop", i => i.crop = crop));
            var land = new MapLand(new Rect(-32f, -32f, 64f, 64f));
            if (withRoad)
            {
                Claim(land, new MapLandRegion(MapLandKind.Road, "Track"), p => Mathf.Abs(p.y) < 1.5f);
            }

            Claim(land, new MapLandRegion(MapLandKind.Field, "Sown", 7, RowAngle), p => Mathf.Abs(p.x) > 4f);
            return (MapCropPainter.Paint(land, MapSymmetry.Parse(MapSymmetry.MirrorXName), climate, blocked), crop, land);
        }
    }
}
