using HyperRTS.Presentation.Environment.RowCrops;
using NUnit.Framework;
using UnityEngine;

namespace HyperRTS.Presentation.Tests
{
    /// <summary>Crop tile culling maths: box distances and the distance bands a tile can hold.</summary>
    public class RowCropViewTests
    {
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

        [Test]
        public void Distances_AreToTheNearestAndFarthestPointOfTheBox()
        {
            var bounds = RowCropView.TileBounds(new Rect(100f, 0f, 16f, 16f), 0f, 0f, 2f);
            var (near, far) = RowCropView.Distances(bounds, new Vector3(0f, 1f, 8f));
            Assert.AreEqual(100f - RowCropView.Overhang, near, 1e-4f);
            Assert.Greater(far, 116f);
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
    }
}
