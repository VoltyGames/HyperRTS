using HyperRTS.Presentation.Environment.RowCrops;
using NUnit.Framework;
using UnityEngine;

namespace HyperRTS.Presentation.Tests
{
    /// <summary>Per-plant variety from the hash: model choice, lean, row wander and growth variation.</summary>
    public class RowCropVarietyTests
    {
        private RowCrop _crop;

        [SetUp]
        public void SetUp() => _crop = ScriptableObject.CreateInstance<RowCrop>();

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_crop);

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

            foreach (var count in used)
            {
                Assert.That(count, Is.InRange(700, 1100), "each of four models takes about a quarter of 3600 plants");
            }
        }

        [Test]
        public void Variant_OfOneModelIsAlwaysIt()
        {
            for (var plant = 0; plant < 200; plant++)
            {
                Assert.AreEqual(0, RowCropSpots.Variant(3, 1, plant, 1));
            }
        }

        [Test]
        public void Wander_StaysInRangeAndTurnsSlowly()
        {
            _crop.wander = 0.1f;
            _crop.wanderLength = 6f;
            var field = Field();
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
            var field = Field();
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
            var field = Field();
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

        private static RowCropField Field() => new()
        {
            origin = new Vector2(1.5f, -4f),
            along = Vector2.up,
            across = Vector2.right,
            seed = 23,
        };
    }
}
