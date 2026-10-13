using System.Collections.Generic;
using HyperRTS.Presentation.Environment.RowCrops;
using NUnit.Framework;
using UnityEngine;

namespace HyperRTS.Presentation.Tests
{
    /// <summary>The row lattice split into tiles: every spot grows in exactly one tile, from the threads its job dispatches.</summary>
    public class RowCropLatticeTests
    {
        private const float RowSpacing = 0.75f;
        private const float PlantSpacing = 0.3f;
        private const float Tile = RowCropFields.TileSize;

        [TestCase(0f)]
        [TestCase(30f)]
        [TestCase(90f)]
        [TestCase(137.5f)]
        public void Tiles_GrowEverySpotOnce(float angle)
        {
            var field = Field(angle);
            var owners = new Dictionary<(int, int), int>();
            for (var tz = 0; tz < 3; tz++)
            {
                for (var tx = 0; tx < 3; tx++)
                {
                    var tile = new Rect(tx * Tile, tz * Tile, Tile, Tile);
                    foreach (var spot in Grown(tile, field))
                    {
                        owners[spot] = owners.TryGetValue(spot, out var count) ? count + 1 : 1;
                    }
                }
            }

            var area = new Rect(0f, 0f, Tile * 3f, Tile * 3f);
            var expected = 0;
            for (var row = -200; row <= 200; row++)
            {
                for (var plant = -400; plant <= 400; plant++)
                {
                    var spot = RowCropLattice.Spot(field, row, plant, RowSpacing, PlantSpacing);
                    if (!RowCropLattice.Owns(area, spot))
                    {
                        continue;
                    }

                    expected++;
                    Assert.IsTrue(owners.TryGetValue((row, plant), out var count), $"spot ({row}, {plant}) at {spot} grew nowhere");
                    Assert.AreEqual(1, count, $"spot ({row}, {plant}) at {spot}");
                }
            }

            Assert.AreEqual(expected, owners.Count);
        }

        [Test]
        public void Job_SpotsStayWithinATurnedTile()
        {
            var rows = Mathf.CeilToInt(Tile * Mathf.Sqrt(2f) / RowSpacing) + 1;
            var plants = Mathf.CeilToInt(Tile * Mathf.Sqrt(2f) / PlantSpacing) + 1;
            for (var angle = 0f; angle < 360f; angle += 7.5f)
            {
                var job = new RowCropJob(new Rect(Tile, -Tile, Tile, Tile), 0, Field(angle), RowSpacing, PlantSpacing);
                Assert.LessOrEqual(job.count.x, rows, $"rows at {angle}°");
                Assert.LessOrEqual(job.count.y, plants, $"plants at {angle}°");
                Assert.AreEqual(job.count.x * job.count.y, job.Spots);
            }
        }

        [Test]
        public void PlantBound_CoversATileOfOneField()
        {
            var bound = RowCropLattice.PlantBound(Tile, RowSpacing, PlantSpacing);
            var tile = new Rect(0f, 0f, Tile, Tile);
            for (var angle = 0f; angle < 180f; angle += 11f)
            {
                Assert.LessOrEqual(Grown(tile, Field(angle)).Count, bound, $"at {angle}°");
            }
        }

        [Test]
        public void FieldIds_RoundTripThroughThePicture()
        {
            foreach (var id in new[] { 0, 1, 255, 256, 4097, 65534 })
            {
                Assert.AreEqual(id, RowCropFields.Decode(RowCropFields.Encode(id)));
            }
        }

        // The spots RowCrops.compute's Plant keeps, one thread per spot of the job.
        private static List<(int, int)> Grown(Rect tile, RowCropField field)
        {
            var grown = new List<(int, int)>();
            var job = new RowCropJob(tile, 0, field, RowSpacing, PlantSpacing);
            for (var thread = 0; thread < job.Spots; thread++)
            {
                var row = job.first.x + thread % job.count.x;
                var plant = job.first.y + thread / job.count.x;
                if (RowCropLattice.Owns(tile, RowCropLattice.Spot(field, row, plant, RowSpacing, PlantSpacing)))
                {
                    grown.Add((row, plant));
                }
            }

            return grown;
        }

        private static RowCropField Field(float angle)
        {
            var radians = angle * Mathf.Deg2Rad;
            var along = new Vector2(Mathf.Sin(radians), Mathf.Cos(radians));
            return new RowCropField
            {
                origin = new Vector2(3.1f, -2.7f),
                along = along,
                across = new Vector2(along.y, -along.x),
                seed = 11,
            };
        }
    }
}
