using System.Collections.Generic;
using HyperRTS.Presentation.Environment.RowCrops;
using NUnit.Framework;
using UnityEngine;

namespace HyperRTS.Presentation.Tests.Environment
{
    /// <summary>Row crops: the row lattice split into tiles, per-plant variety from the hash, distance bands and GPU layouts.</summary>
    public class RowCropTests
    {
        private const float RowSpacing = 0.75f;
        private const float PlantSpacing = 0.3f;
        private const float Tile = RowCropFields.TileSize;

        private RowCrop _crop;

        [SetUp]
        public void SetUp()
        {
            _crop = ScriptableObject.CreateInstance<RowCrop>();
            _crop.lod0Distance = 40f;
            _crop.lod1Distance = 120f;
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_crop);

        [TestCase(0f)]
        [TestCase(30f)]
        [TestCase(137.5f)]
        public void Tiles_GrowEverySpotOnce(float angle)
        {
            var field = Field(angle);
            var owners = new Dictionary<(int, int), int>();
            for (var tz = 0; tz < 3; tz++)
            {
                for (var tx = 0; tx < 3; tx++)
                {
                    foreach (var spot in Grown(new Rect(tx * Tile, tz * Tile, Tile, Tile), field))
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
                    var spot = RowCropSpots.Spot(field, row, plant, RowSpacing, PlantSpacing);
                    if (!RowCropSpots.Owns(area, spot))
                    {
                        continue;
                    }

                    expected++;
                    Assert.IsTrue(owners.TryGetValue((row, plant), out var count), $"spot ({row}, {plant}) grew nowhere");
                    Assert.AreEqual(1, count, $"spot ({row}, {plant}) at {spot}");
                }
            }

            Assert.AreEqual(expected, owners.Count);
        }

        [Test]
        public void Job_SpotsStayWithinATurnedTile_AndUnderThePlantBound()
        {
            var rows = Mathf.CeilToInt(Tile * Mathf.Sqrt(2f) / RowSpacing) + 1;
            var plants = Mathf.CeilToInt(Tile * Mathf.Sqrt(2f) / PlantSpacing) + 1;
            var bound = RowCropSpots.PlantBound(Tile, RowSpacing, PlantSpacing);
            var tile = new Rect(Tile, -Tile, Tile, Tile);
            for (var angle = 0f; angle < 360f; angle += 7.5f)
            {
                var job = new RowCropJob(tile, 0, Field(angle), RowSpacing, PlantSpacing);
                Assert.LessOrEqual(job.count.x, rows, $"rows at {angle} degrees");
                Assert.LessOrEqual(job.count.y, plants, $"plants at {angle} degrees");
                Assert.AreEqual(job.count.x * job.count.y, job.Spots);
                Assert.LessOrEqual(Grown(tile, Field(angle)).Count, bound, $"at {angle} degrees");
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

        [Test]
        public void GpuStructs_MatchTheKernelsLayouts()
        {
            Assert.AreEqual(48, RowCropField.Stride);
            Assert.AreEqual(28, RowCropJob.Stride);
        }

        [Test]
        public void Variant_IsStableAndUsesEveryModel()
        {
            var used = new int[4];
            for (var row = -30; row < 30; row++)
            {
                for (var plant = -30; plant < 30; plant++)
                {
                    var variant = RowCropSpots.Variant(7, row, plant, used.Length);
                    Assert.AreEqual(variant, RowCropSpots.Variant(7, row, plant, used.Length));
                    used[variant]++;
                }
            }

            Assert.That(used, Is.All.InRange(700, 1100), "each of four models takes about a quarter of 3600 plants");
            Assert.AreEqual(0, RowCropSpots.Variant(3, 1, 17, 1), "one model is always it");
        }

        [Test]
        public void Wander_StaysInRangeAndTurnsSlowly()
        {
            _crop.wander = 0.1f;
            _crop.wanderLength = 6f;
            var field = Field(0f);
            var steps = Mathf.CeilToInt(30f / _crop.plantSpacing);
            for (var plant = 0; plant < steps; plant++)
            {
                var here = RowCropSpots.Wander(field, 4, plant, _crop);
                var next = RowCropSpots.Wander(field, 4, plant + 1, _crop);
                var beside = RowCropSpots.Wander(field, 5, plant, _crop);
                Assert.LessOrEqual(Mathf.Abs(here), _crop.wander + 1e-5f);
                Assert.Less(Mathf.Abs(next - here), 0.025f, "one plant along the row");
                Assert.Less(Mathf.Abs(beside - here), 0.05f, "the next row wanders with this one");
            }
        }

        [Test]
        public void GrowthVariation_StaysInItsRange()
        {
            _crop.growthVariation = new Vector2(0.8f, 1.2f);
            var field = Field(0f);
            for (var row = 0; row < 40; row++)
            {
                for (var plant = 0; plant < 40; plant += 3)
                {
                    Assert.That(RowCropSpots.GrowthVariation(field, row, plant, _crop), Is.InRange(0.8f, 1.2f));
                }
            }
        }

        [Test]
        public void Lean_IsMostAtTheEdge()
        {
            _crop.lean = 2f;
            _crop.edgeLean = 10f;
            var field = Field(0f);
            var inside = Mathf.Sin(_crop.lean * Mathf.Deg2Rad) + 1e-5f;
            var edge = Mathf.Sin(_crop.edgeLean * Mathf.Deg2Rad) + 1e-5f;
            var widest = 0f;
            for (var plant = 0; plant < 400; plant++)
            {
                Assert.LessOrEqual(RowCropSpots.Lean(field, 2, plant, _crop, 0f).magnitude, inside);
                var atEdge = RowCropSpots.Lean(field, 2, plant, _crop, 1f).magnitude;
                Assert.LessOrEqual(atEdge, edge);
                widest = Mathf.Max(widest, atEdge);
            }

            Assert.Greater(widest, inside, "edge plants lean past the inside limit");
        }

        [Test]
        public void Problems_NameAnEmptyVariant()
        {
            _crop.variants.Add(new RowCropVariant());
            CollectionAssert.Contains(_crop.Problems(), "variants[0].lod0 is missing");
            CollectionAssert.Contains(_crop.Problems(), "variants[0].card is missing");
        }

        [TestCase(0f, 30f, true, false, false, TestName = "Near tile: full meshes only")]
        [TestCase(30f, 50f, true, true, false, TestName = "Across the first switch: full and simple")]
        [TestCase(110f, 130f, false, true, true, TestName = "Across the second switch: simple and cards")]
        [TestCase(200f, 220f, false, false, true, TestName = "Far tile: cards only")]
        public void Bands_FollowTheTilesDistances(float near, float far, bool full, bool simple, bool card)
        {
            Assert.AreEqual(full, RowCropView.InBand(RowCropView.Full, _crop, 300f, near, far));
            Assert.AreEqual(simple, RowCropView.InBand(RowCropView.Simple, _crop, 300f, near, far));
            Assert.AreEqual(card, RowCropView.InBand(RowCropView.Card, _crop, 300f, near, far));
        }

        [Test]
        public void Bands_PastTheDrawDistance_AreEmpty()
        {
            Assert.IsFalse(RowCropView.InBand(RowCropView.Card, _crop, 100f, 0f, 500f));
            Assert.IsFalse(RowCropView.InBand(RowCropView.Simple, _crop, 30f, 0f, 500f));
            Assert.IsTrue(RowCropView.InBand(RowCropView.Full, _crop, 30f, 0f, 500f));
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
                if (RowCropSpots.Owns(tile, RowCropSpots.Spot(field, row, plant, RowSpacing, PlantSpacing)))
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
