using HyperRTS.Presentation.Rendering;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace HyperRTS.Presentation.Tests.Rendering
{
    /// <summary>Sharpen settings checks and the quality levels it runs at.</summary>
    public class SharpenFeatureTests
    {
        private SharpenFeature _feature;

        [SetUp]
        public void SetUp()
        {
            // A new feature creates itself, and reports its missing shader, while URP is the running pipeline.
            LogAssert.ignoreFailingMessages = true;
            _feature = ScriptableObject.CreateInstance<SharpenFeature>();
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_feature);

        [Test]
        public void SettingsProblem_DefaultSettings_IsEmpty() => Assert.IsEmpty(_feature.SettingsProblem());

        [Test]
        public void SettingsProblem_StrengthOutOfRange_NamesIt()
        {
            _feature.strength = 1.5f;
            StringAssert.Contains("strength", _feature.SettingsProblem());
            _feature.strength = -0.1f;
            StringAssert.Contains("strength", _feature.SettingsProblem());
        }

        [Test]
        public void RunsAt_TheLowestQualityLevelAndAbove()
        {
            _feature.lowestQualityLevel = 2;
            Assert.IsFalse(_feature.RunsAt(1));
            Assert.IsTrue(_feature.RunsAt(2));
        }
    }
}
