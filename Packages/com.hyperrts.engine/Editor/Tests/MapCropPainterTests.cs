using System.Collections.Generic;
using HyperRTS.Editor.Maps.Data;
using HyperRTS.Editor.Maps.Ground;
using HyperRTS.Editor.Maps.Land;
using HyperRTS.Editor.Maps.Symmetry;
using HyperRTS.Presentation.Environment.RowCrops;
using NUnit.Framework;
using UnityEngine;

namespace HyperRTS.Editor.Tests
{
    public class MapCropPainterTests
    {
        private const int Half = 32;
        private RowCrop _crop;
        private MapClimate _climate;

        [SetUp]
        public void SetUp()
        {
            _crop = ScriptableObject.CreateInstance<RowCrop>();
            _climate = new MapClimate
            {
                name = "Test",
                surfaces = new List<MapClimateSurface>
                {
                    new() { key = "Sown", rowCrop = "Crop" },
                    new() { key = "Track" },
                },
                rowCrops = new List<MapClimateRowCrop> { new() { key = "Crop", crop = _crop } },
            };
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_crop);

        [Test]
        public void MirroredCopies_SowMirroredIdsAndPlants()
        {
            var land = Land(withRoad: false);
            var bake = MapCropPainter.Paint(land, MapSymmetry.Parse(MapSymmetry.MirrorXName), _climate, (_, _, _) => false);

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
                    var a = RowCropSpots.Position(west, row, plant, _crop);
                    var b = RowCropSpots.Position(east, row, plant, _crop);
                    Assert.AreEqual(-a.x, b.x, 1e-4f);
                    Assert.AreEqual(a.y, b.y, 1e-4f);
                }
            }
        }

        [Test]
        public void Roads_AndBlockedSpots_StayBare()
        {
            var land = Land(withRoad: true);
            var bake = MapCropPainter.Paint(land, MapSymmetry.Parse(MapSymmetry.MirrorXName), _climate,
                (point, _, _) => point.x is > -20f and < -18f);

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
        public void Tiles_ListEveryCropCellsField()
        {
            var bake = MapCropPainter.Paint(Land(withRoad: true), MapSymmetry.Parse(MapSymmetry.MirrorXName), _climate,
                (_, _, _) => false);
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
                    var fields = bake.TileFields.GetRange(tile.firstField, tile.fieldCount);
                    CollectionAssert.Contains(fields, id - 1);
                }
            }
        }

        /// <summary>A field across both halves of a 64 m map, rows at 30°, with an east-west road through it.</summary>
        private static MapLand Land(bool withRoad)
        {
            var land = new MapLand(new Rect(-Half, -Half, Half * 2, Half * 2));
            if (withRoad)
            {
                var road = land.Add(new MapLandRegion(MapLandKind.Road, "Track"));
                land.ClaimWhere(land.Bounds, road, p => Mathf.Abs(p.y) < 1.5f);
            }

            var field = land.Add(new MapLandRegion(MapLandKind.Field, "Sown", 7, 30f));
            land.ClaimWhere(land.Bounds, field, p => Mathf.Abs(p.x) > 4f);
            return land;
        }
    }
}
