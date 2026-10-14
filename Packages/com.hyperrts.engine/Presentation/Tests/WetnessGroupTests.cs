using HyperRTS.Presentation.Environment;
using NUnit.Framework;
using UnityEngine;

namespace HyperRTS.Presentation.Tests
{
    public class WetnessGroupTests
    {
        private GameObject _group;
        private GameObject _puddle;

        [SetUp]
        public void SetUp()
        {
            Wetness.Publish(0f);
            _group = new GameObject("Wet Decals");
            _puddle = new GameObject("Puddle 1");
            _puddle.transform.SetParent(_group.transform, false);
            _group.AddComponent<WetnessGroup>().threshold = 0.5f;
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_group);
            Wetness.Publish(0f);
        }

        [Test]
        public void DryGround_HidesChildren()
        {
            _group.GetComponent<WetnessGroup>().Apply();

            Assert.IsFalse(_puddle.activeSelf);
        }

        [Test]
        public void Wetness_ShowsChildrenFromTheThreshold_AndHidesThemAgainWhenDry()
        {
            Wetness.Publish(0.4f);
            Assert.IsFalse(_puddle.activeSelf);

            Wetness.Publish(0.8f);
            Assert.IsTrue(_puddle.activeSelf);

            Wetness.Publish(0f);
            Assert.IsFalse(_puddle.activeSelf);
        }

        [Test]
        public void DisabledGroup_StopsListening()
        {
            _group.GetComponent<WetnessGroup>().enabled = false;

            Wetness.Publish(0.8f);

            Assert.IsFalse(_puddle.activeSelf);
        }
    }
}
