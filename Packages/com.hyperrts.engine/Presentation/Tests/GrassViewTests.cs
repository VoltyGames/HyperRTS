using HyperRTS.Presentation.Environment.Grass;
using NUnit.Framework;
using UnityEngine;

namespace HyperRTS.Presentation.Tests
{
    /// <summary>The grass kernel's CPU rules: thinning with distance, bands and buffer room, and the GPU layouts.</summary>
    public class GrassViewTests
    {
        [Test]
        public void Keep_IsFullNear_AndThinsSteadilyToTheFarShareAtTheDistance()
        {
            Assert.AreEqual(1f, GrassView.Keep(0f, 200f));
            Assert.AreEqual(1f, GrassView.Keep(GrassView.ThinStart * 200f, 200f), 1e-5f);
            Assert.AreEqual(GrassView.FarKeep, GrassView.Keep(200f, 200f), 1e-5f);
            var last = 1f;
            for (var range = 0f; range <= 200f; range += 10f)
            {
                var keep = GrassView.Keep(range, 200f);
                Assert.That(keep, Is.LessThanOrEqualTo(last), $"keep at {range} m");
                last = keep;
            }
        }

        [Test]
        public void Grow_CoversForThinnedPlants_UpToItsCap()
        {
            Assert.AreEqual(1f, GrassView.Grow(1f));
            Assert.AreEqual(Mathf.Sqrt(2f), GrassView.Grow(0.5f), 1e-5f);
            Assert.AreEqual(GrassView.MaxGrow, GrassView.Grow(GrassView.FarKeep));
        }

        [Test]
        public void Drawn_NeverExceedsOne_AndCountsTheShrinkingEdge()
        {
            Assert.AreEqual(1f, GrassView.Drawn(0f, 200f));
            var far = GrassView.FarKeep * (1f + GrassView.KeepEdge);
            Assert.AreEqual(far, GrassView.Drawn(200f, 200f), 1e-5f);
        }

        [TestCase(0f, 40f, 30f, true, true)]
        [TestCase(40f, 60f, 30f, false, true)]
        [TestCase(0f, 20f, 30f, true, false)]
        [TestCase(0f, 20f, 0f, false, true)]
        public void Bands_HoldTheTilesThatReachThem(float near, float far, float shadows, bool inNear, bool inFar)
        {
            Assert.AreEqual(inNear, GrassView.InBand(GrassView.Near, shadows, near, far));
            Assert.AreEqual(inFar, GrassView.InBand(GrassView.Far, shadows, near, far));
        }

        [TestCase(0f)]
        [TestCase(10f)]
        [TestCase(5000f)]
        public void Capacity_LeavesRoomOverTheExpectedCount(float expected) =>
            Assert.That(GrassView.Capacity(expected), Is.GreaterThan(expected * 1.05f + 3f));

        [Test]
        public void Structs_MatchTheKernelsLayouts()
        {
            Assert.AreEqual(36, GrassSowing.Stride);
            Assert.AreEqual(28, GrassJob.Stride);
        }

        [TestCase(0f, 0f, 0.29f)]
        [TestCase(-48f, 160f, 0.5f)]
        [TestCase(32f, -16f, 1.3f)]
        public void Jobs_CoverEveryCellWhosePlantCanFallInTheTile(float x, float z, float spacing)
        {
            var tile = new Rect(x, z, GrassField.TileSize, GrassField.TileSize);
            var job = new GrassJob(tile, 3, spacing);

            Assert.AreEqual(3, job.plant);
            Assert.AreEqual(tile.min, job.tileMin);
            for (var probe = 0f; probe < GrassField.TileSize; probe += spacing * 0.37f)
            {
                var cell = new Vector2Int(Mathf.FloorToInt((x + probe) / spacing), Mathf.FloorToInt((z + probe) / spacing));
                var offset = cell - job.first;
                Assert.That(offset.x, Is.InRange(0, job.count.x - 1), $"cell {cell}");
                Assert.That(offset.y, Is.InRange(0, job.count.y - 1), $"cell {cell}");
            }

            Assert.That(job.Cells, Is.LessThanOrEqualTo(Mathf.Pow(GrassField.TileSize / spacing + 2f, 2f)));
        }
    }
}
