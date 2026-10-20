using HyperRTS.Presentation.Effects;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace HyperRTS.Presentation.Tests
{
    public partial class EffectStreamTests
    {
        private static readonly Entity Shell = new() { Index = 1, Version = 1 };

        [Test]
        public void Step_FirstFrameWarmsUp_ThenRunsFromLastFrame()
        {
            var streams = new EffectStreams();

            Assert.IsFalse(Frame(streams, new Vector3(1f, 0f, 0f)).Ready, "the spawn frame's pose may be stale");
            var first = Frame(streams, new Vector3(2f, 0f, 0f));
            Assert.IsTrue(first.Ready);
            Assert.AreEqual(first.To, first.From, "no segment from the warm-up point");

            var second = Frame(streams, new Vector3(3f, 0f, 0f));
            Assert.AreEqual(new Vector3(2f, 0f, 0f), second.From);
            Assert.AreEqual(new Vector3(3f, 0f, 0f), second.To);
        }

        private static StreamState Frame(EffectStreams streams, Vector3 position)
        {
            streams.BeginFrame();
            var state = streams.Step(Shell, 0, position);
            streams.EndFrame();
            return state;
        }

        [Test]
        public void Step_AfterASkippedFrame_StartsAfresh()
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
        public void SpawnedAtTheBakedOrigin_StreamsFromItsRealPosition()
        {
            var streams = new EffectStreams();
            var real = new Vector3(40f, 0f, -25f);

            Assert.IsFalse(Frame(streams, Vector3.zero).Ready, "nothing streams from the stale spawn frame");
            var state = Frame(streams, real);

            Assert.IsTrue(state.Ready);
            Assert.AreEqual(real, state.From, "no segment from the world origin");
            Assert.AreEqual(real, state.To);
        }

        [Test]
        public void StillSpawn_IsCurrentOnlyOnceItsWorldPoseCatchesUp()
        {
            using var world = new World("Stale Pose Tests");
            var em = world.EntityManager;
            var probe = world.CreateSystemManaged<PoseProbe>();
            var real = new float3(40f, 0f, -25f);
            var spawned = em.CreateEntity(typeof(LocalTransform), typeof(LocalToWorld));
            em.SetComponentData(spawned, LocalTransform.FromPosition(real));
            em.SetComponentData(spawned, new LocalToWorld { Value = float4x4.identity });
            var child = em.CreateEntity(typeof(LocalTransform), typeof(LocalToWorld), typeof(Parent));
            em.SetComponentData(child, LocalTransform.FromPosition(new float3(1f, 0f, 0f)));
            em.SetComponentData(child, new LocalToWorld { Value = float4x4.Translate(real) });

            Assert.IsFalse(probe.Current(spawned), "the baked origin pose of an ECB spawn");
            Assert.IsTrue(probe.Current(child), "children sit off their parent");

            em.SetComponentData(spawned, new LocalToWorld { Value = float4x4.Translate(real) });
            Assert.IsTrue(probe.Current(spawned));
        }

        [DisableAutoCreation]
        private partial class PoseProbe : SystemBase
        {
            public bool Current(Entity entity) => EffectStreams.PoseCurrent(entity,
                EntityManager.GetComponentData<LocalToWorld>(entity), GetComponentLookup<LocalTransform>(true),
                GetComponentLookup<Parent>(true));

            protected override void OnUpdate()
            {
            }
        }

        [Test]
        public void VarietyKey_IsStablePerEntityAndStream()
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
    }
}
