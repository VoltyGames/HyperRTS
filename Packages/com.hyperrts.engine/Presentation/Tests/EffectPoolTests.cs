using System.Collections.Generic;
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
        public void Pick_PlaysEachVariantAsItsOwnPooledRig()
        {
            _dust.variants = new[] { _sparks.gameObject };

            var dust = _pool.Get(_dust.gameObject);
            var sparks = _pool.Get(_sparks.gameObject);

            Assert.AreSame(dust, _pool.Get(_dust.gameObject).Pick(0u));
            Assert.AreSame(sparks, _pool.Get(_dust.gameObject).Pick(1u));
            Assert.AreSame(sparks, _pool.Get(_dust.gameObject).Pick(1u), "the same key keeps its variant");
        }

        [Test]
        public void Pick_ForABurst_ReachesEveryVariant()
        {
            _dust.variants = new[] { _sparks.gameObject };
            var played = new HashSet<EffectRig>();

            for (var i = 0; i < 64; i++)
            {
                played.Add(_pool.Get(_dust.gameObject).Pick());
            }

            Assert.AreEqual(2, played.Count);
        }

        [Test]
        public void Get_VariantWithVariants_LogsAndReturnsNull()
        {
            _dust.variants = new[] { _sparks.gameObject };
            _sparks.variants = new[] { _dust.gameObject };

            LogAssert.Expect(LogType.Error, new Regex("Dust.*variants of its own"));
            Assert.IsNull(_pool.Get(_dust.gameObject));
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
            var low = EffectDetail.DensityAt(DetailLevel.Low);
            var medium = EffectDetail.DensityAt(DetailLevel.Medium);
            Assert.Greater(low, 0f, "low thins effects, never empties them");
            Assert.Less(low, medium);
            Assert.Less(medium, EffectDetail.DensityAt(DetailLevel.High));
            Assert.AreEqual(1f, EffectDetail.DensityAt(DetailLevel.High), "high plays rigs as authored");
        }
    }
}
