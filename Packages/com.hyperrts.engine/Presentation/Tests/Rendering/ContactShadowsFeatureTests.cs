using HyperRTS.Presentation.Rendering;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace HyperRTS.Presentation.Tests.Rendering
{
    /// <summary>Contact shadow settings checks and the quality levels they run at.</summary>
    public class ContactShadowsFeatureTests
    {
        private ContactShadowsFeature _feature;

        [SetUp]
        public void SetUp()
        {
            // A new feature creates itself, and reports its missing shader, while URP is the running pipeline.
            LogAssert.ignoreFailingMessages = true;
            _feature = ScriptableObject.CreateInstance<ContactShadowsFeature>();
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_feature);

        [Test]
        public void SettingsProblem_DefaultSettings_IsEmpty() => Assert.IsEmpty(_feature.SettingsProblem());

        [Test]
        public void SettingsProblem_NamesTheBadSetting()
        {
            _feature.length = 0f;
            StringAssert.Contains("length", _feature.SettingsProblem());
            _feature.length = 1f;
            _feature.steps = 0;
            StringAssert.Contains("steps", _feature.SettingsProblem());
            _feature.steps = 12;
            _feature.thickness = -1f;
            StringAssert.Contains("thickness", _feature.SettingsProblem());
            _feature.thickness = 0.4f;
            _feature.strength = 1.5f;
            StringAssert.Contains("strength", _feature.SettingsProblem());
            _feature.strength = 1f;
            _feature.fadeEnd = _feature.fadeStart;
            StringAssert.Contains("fade", _feature.SettingsProblem());
            _feature.fadeStart = -1f;
            StringAssert.Contains("fade", _feature.SettingsProblem());
        }
    }
}
