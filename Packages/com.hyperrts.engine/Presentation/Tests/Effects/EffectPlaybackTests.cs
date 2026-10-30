using System.Collections.Generic;
using System.Text.RegularExpressions;
using HyperRTS.Presentation.Effects;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Cues;
using HyperRTS.Simulation.Navigation;
using HyperRTS.Simulation.Tests;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.TestTools;

namespace HyperRTS.Presentation.Tests.Effects
{
    /// <summary>Effect playback around the rigs: the pool and its variants, per-entity streams and impact poses.</summary>
    public class EffectPlaybackTests
    {
        private static readonly Entity Shell = new() { Index = 1, Version = 1 };
        private static readonly Vector3 RampNormal = new Vector3(-1f, 1f, 0f).normalized;

        private EffectPool _pool;
        private EffectRig _dust;
        private EffectRig _sparks;
        private TerrainHeight _ramp;

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
            if (_ramp.Blob.IsCreated)
            {
                _ramp.Blob.Dispose();
            }
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
        public void Get_VariantWithVariants_LogsAndReturnsNull()
        {
            _dust.variants = new[] { _sparks.gameObject };
            _sparks.variants = new[] { _dust.gameObject };

            LogAssert.Expect(LogType.Error, new Regex("Dust.*variants of its own"));
            Assert.IsNull(_pool.Get(_dust.gameObject));
        }

        [Test]
        public void Pick_PlaysEachVariantAsItsOwnPooledRig_StablePerKey_AndBurstsReachEvery()
        {
            _dust.variants = new[] { _sparks.gameObject };
            var dust = _pool.Get(_dust.gameObject);
            var sparks = _pool.Get(_sparks.gameObject);

            Assert.AreSame(dust, dust.Pick(0u));
            Assert.AreSame(sparks, dust.Pick(1u));
            Assert.AreSame(sparks, dust.Pick(1u), "the same key keeps its variant");
            Assert.AreSame(dust, dust.Pick(2u));

            var played = new HashSet<EffectRig>();
            for (var i = 0; i < 64; i++)
            {
                played.Add(dust.Pick());
            }

            Assert.AreEqual(2, played.Count);
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
            Assert.AreEqual(1f, EffectDetail.DensityAt(DetailLevel.High), "high plays rigs as authored");
        }

        [Test]
        public void Stream_FirstFrameHasNoSegment_ThenRunsFromLastFrame()
        {
            var streams = new EffectStreams();

            var first = Frame(streams, new Vector3(2f, 0f, 0f));
            Assert.AreEqual(first.To, first.From);
            Assert.AreEqual(EffectStreams.Key(Shell, 0), first.Key);

            var second = Frame(streams, new Vector3(3f, 0f, 0f));
            Assert.AreEqual(new Vector3(2f, 0f, 0f), second.From);
            Assert.AreEqual(new Vector3(3f, 0f, 0f), second.To);
        }

        [Test]
        public void Stream_AfterASkippedFrame_StartsAfresh()
        {
            var streams = new EffectStreams();
            streams.BeginFrame();
            streams.Step(Shell, 0, Vector3.zero).Carry = 0.7f;
            streams.EndFrame();
            streams.BeginFrame();
            streams.EndFrame();

            streams.BeginFrame();
            var state = streams.Step(Shell, 0, new Vector3(50f, 0f, 0f));

            Assert.AreEqual(state.To, state.From, "no segment across the gap");
            Assert.AreEqual(0f, state.Carry);
        }

        [Test]
        public void SkippedStream_StopsItsGraphs_WhichTheNextStreamReuses()
        {
            EffectRigKit.AddGraph(_dust, EffectRigKit.TestStreamGraph);
            var streams = new EffectStreams();
            streams.BeginFrame();
            var state = streams.Step(Shell, 0, Vector3.zero);
            state.Use(_dust);
            state.Emit(Quaternion.identity, 0.1f, 1f);
            var graphs = state.Graphs;
            streams.EndFrame();
            Assert.IsNotNull(graphs);

            streams.BeginFrame();
            streams.EndFrame();
            _dust.Tick(0f);

            Assert.IsNull(state.Graphs, "stopped with its stream");
            Assert.AreSame(graphs, _dust.StartGraphs(), "a set with no live particles serves the next stream");
        }

        [Test]
        public void StreamKey_IsStablePerEntityAndStream()
        {
            var other = new Entity { Index = 2, Version = 1 };

            Assert.AreEqual(EffectStreams.Key(Shell, 0), EffectStreams.Key(Shell, 0));
            Assert.AreNotEqual(EffectStreams.Key(Shell, 0), EffectStreams.Key(Shell, 1));
            Assert.AreNotEqual(EffectStreams.Key(Shell, 0), EffectStreams.Key(other, 0));
        }

        [Test]
        public void LoopFade_HoldsThenFadesOverTheEnd()
        {
            Assert.AreEqual(1f, LoopFade.Strength(100f, 0f), "no duration streams forever");
            Assert.AreEqual(1f, LoopFade.Strength(5f, 10f));
            Assert.AreEqual(0.5f, LoopFade.Strength(9f, 10f), 1e-4f);
            Assert.AreEqual(0f, LoopFade.Strength(11f, 10f));
        }

        [Test]
        public void Facing_UpASlope_LooksAlongIt_WithoutRoll()
        {
            var direction = new Vector3(1f, 1f, 0f);

            var facing = EffectPose.Facing(direction);

            MathAssert.Near(direction.normalized, facing * Vector3.forward);
            Assert.AreEqual(0f, (facing * Vector3.right).y, 1e-5f, "kept upright");
            Assert.Greater((facing * Vector3.up).y, 0f);
            Assert.AreEqual(Quaternion.identity, EffectPose.Facing(Vector3.zero));
        }

        [TestCase(true)]
        [TestCase(false)]
        public void GroundImpact_SitsOnTheSlope_UpAlongItsNormalOnlyWhenAligned(bool aligned)
        {
            var (position, rotation) = EffectPose.Impact(Hit(ImpactSurface.Ground), Ramp(), true, aligned);

            MathAssert.Near(new Vector3(5f, 5f, 5f), position);
            MathAssert.Near(aligned ? RampNormal : Vector3.up, rotation * Vector3.up);
        }

        [Test]
        public void OffTheGround_KeepsTheCuePosition_Unturned()
        {
            var air = EffectPose.Impact(Hit(ImpactSurface.Air), Ramp(), true, true);
            var noTerrain = EffectPose.Impact(Hit(ImpactSurface.Ground), _ramp, false, true);

            MathAssert.Near(new Vector3(5f, 9f, 5f), air.Position);
            MathAssert.Near(new Vector3(5f, 9f, 5f), noTerrain.Position);
            Assert.AreEqual(Quaternion.identity, air.Rotation);
            Assert.AreEqual(Quaternion.identity, noTerrain.Rotation);
        }

        // A 45 degree ramp rising along x: height = x.
        private TerrainHeight Ramp()
        {
            using var heights = new NativeArray<float>(new[] { 0f, 10f, 0f, 10f }, Allocator.Temp);
            _ramp = TerrainHeight.Create(heights, new int2(2, 2), float2.zero, 10f, Allocator.Persistent);
            return _ramp;
        }

        private static StreamState Frame(EffectStreams streams, Vector3 position)
        {
            streams.BeginFrame();
            var state = streams.Step(Shell, 0, position);
            streams.EndFrame();
            return state;
        }

        private static CueEvent Hit(ImpactSurface surface) =>
            new() { Slot = CueSlot.Impact, Position = new float3(5f, 9f, 5f), Surface = surface };
    }
}
