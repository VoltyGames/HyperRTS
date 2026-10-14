using System.Text.RegularExpressions;
using HyperRTS.Presentation.Effects;
using HyperRTS.Simulation.Common;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace HyperRTS.Presentation.Tests
{
    public class EffectPoolTests
    {
        private EffectPool _pool;
        private EffectRig _dust;
        private EffectRig _sparks;

        [SetUp]
        public void SetUp()
        {
            _pool = new EffectPool();
            _dust = EffectRigKit.Rig("Dust");
            _sparks = EffectRigKit.Rig("Sparks");
        }

        [TearDown]
        public void TearDown()
        {
            _pool.Dispose();
            Object.DestroyImmediate(_dust.gameObject);
            Object.DestroyImmediate(_sparks.gameObject);
        }

        [Test]
        public void Get_ReturnsOneLiveInstancePerPrefab()
        {
            var dust = _pool.Get(_dust.gameObject);

            Assert.AreSame(dust, _pool.Get(_dust.gameObject));
            Assert.AreNotSame(_dust, dust, "an instance, not the prefab");
            Assert.AreNotSame(dust, _pool.Get(_sparks.gameObject));
        }

        [Test]
        public void Get_PrefabWithoutRig_LogsOnceAndReturnsNull()
        {
            var plain = new GameObject("Plain Smoke");
            try
            {
                LogAssert.Expect(LogType.Error, new Regex("Plain Smoke"));
                Assert.IsNull(_pool.Get(plain));
                Assert.IsNull(_pool.Get(plain), "the second miss stays quiet");
            }
            finally
            {
                Object.DestroyImmediate(plain);
            }
        }

        [Test]
        public void Get_LocalSpaceRig_LogsNamingThePrefabAndReturnsNull()
        {
            var local = EffectRigKit.Rig("Local Smoke", ParticleSystemSimulationSpace.Local);
            try
            {
                LogAssert.Expect(LogType.Error, new Regex("Local Smoke.*World space"));
                Assert.IsNull(_pool.Get(local.gameObject));
            }
            finally
            {
                Object.DestroyImmediate(local.gameObject);
            }
        }

        [Test]
        public void Tick_FiresQueuedTimedBursts()
        {
            EffectRigKit.AddBurst(_dust, 0.2f, 4);
            var dust = _pool.Get(_dust.gameObject);

            dust.Burst(Vector3.zero, Quaternion.identity, 1f, 1f);
            _pool.Tick(0.25f);

            Assert.AreEqual(4, EffectRigKit.System(dust).particleCount);
        }

        [Test]
        public void Density_ThinsByDetailLevel()
        {
            Assert.AreEqual(0.35f, EffectDetail.DensityAt(DetailLevel.Low));
            Assert.AreEqual(0.7f, EffectDetail.DensityAt(DetailLevel.Medium));
            Assert.AreEqual(1f, EffectDetail.DensityAt(DetailLevel.High));
        }
    }
}
