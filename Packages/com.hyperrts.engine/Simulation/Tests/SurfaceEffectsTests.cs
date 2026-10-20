using HyperRTS.Simulation.Cues;
using NUnit.Framework;
using UnityEngine;

namespace HyperRTS.Simulation.Tests
{
    /// <summary>Which rig a <see cref="SurfaceEffects"/> picks for a material: its own entry, else the catch-all.</summary>
    public class SurfaceEffectsTests
    {
        private SurfaceEffects _effects;
        private GameObject _splash;
        private GameObject _dust;

        [SetUp]
        public void SetUp()
        {
            _effects = ScriptableObject.CreateInstance<SurfaceEffects>();
            _splash = new GameObject("Splash");
            _dust = new GameObject("Dust");
            _effects.materials = new[] { new SurfaceEffects.Entry { material = SurfaceMaterial.Water, rig = _splash } };
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_effects);
            Object.DestroyImmediate(_splash);
            Object.DestroyImmediate(_dust);
        }

        [Test]
        public void MaterialWithAnEntry_GetsItsOwnRig()
        {
            _effects.anyMaterial = _dust;
            Assert.AreSame(_splash, _effects.RigFor(SurfaceMaterial.Water));
        }

        [Test]
        public void MaterialWithoutAnEntry_GetsTheCatchAll()
        {
            _effects.anyMaterial = _dust;
            Assert.AreSame(_dust, _effects.RigFor(SurfaceMaterial.Soil));
        }

        [Test]
        public void MaterialWithoutAnEntryOrCatchAll_GetsNoRig() =>
            Assert.IsNull(_effects.RigFor(SurfaceMaterial.Soil));
    }
}
