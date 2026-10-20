using HyperRTS.Presentation.Effects;
using NUnit.Framework;
using Unity.Entities;
using UnityEngine;

namespace HyperRTS.Presentation.Tests
{
    public class EffectStreamTests
    {
        private static readonly Entity Shell = new() { Index = 1, Version = 1 };

        [Test]
        public void Step_FirstFrameHasNoSegment_ThenRunsFromLastFrame()
        {
            var streams = new EffectStreams();

            var first = Frame(streams, new Vector3(2f, 0f, 0f));
            Assert.AreEqual(first.To, first.From);

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
