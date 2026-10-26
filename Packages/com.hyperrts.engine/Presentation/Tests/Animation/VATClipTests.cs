using System.Collections.Generic;
using System.IO;
using HyperRTS.Presentation.Animation;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;

namespace HyperRTS.Presentation.Tests.Animation
{
    /// <summary>Vertex animation clips: frame blending, looping and holding, clip changes and the bake info import.</summary>
    public class VATClipTests
    {
        private const string BakeInfoJson = @"{
            ""name"": ""Rifleman"",
            ""width"": 1802,
            ""rowsPerFrame"": 2,
            ""clips"": [
                { ""name"": ""Idle"", ""startRow"": 0, ""frameCount"": 30, ""fps"": 30, ""loop"": true },
                { ""name"": ""Die"", ""startRow"": 30, ""frameCount"": 21, ""fps"": 24, ""loop"": false }
            ],
            ""boundsMin"": [-0.5, 0, -0.4],
            ""boundsMax"": [0.5, 1.9, 0.6]
        }";

        private static readonly VATClipRange Walk = new() { StartRow = 10, FrameCount = 4, Fps = 2f, Loop = true };
        private static readonly VATClipRange Fall = new() { StartRow = 20, FrameCount = 4, Fps = 2f, Loop = false };

        [Test]
        public void FrameAt_BlendsBetweenFramesFromTheStartTime()
        {
            var frame = VATClip.Started(Walk, 5f).FrameAt(5.75f);

            Assert.AreEqual(11, frame.Frame);
            Assert.AreEqual(12, frame.NextFrame);
            Assert.AreEqual(0.5f, frame.Blend, 1e-5f);
        }

        [Test]
        public void FrameAt_LoopWrapsTheLastFrameIntoTheFirst()
        {
            var clip = VATClip.Started(Walk, 0f);

            var last = clip.FrameAt(1.75f);
            var wrapped = clip.FrameAt(2.25f);

            Assert.AreEqual((13, 10), (last.Frame, last.NextFrame));
            Assert.AreEqual((10, 11), (wrapped.Frame, wrapped.NextFrame));
        }

        [Test]
        public void FrameAt_OnceHoldsTheLastFrame()
        {
            var frame = VATClip.Started(Fall, 0f).FrameAt(100f);

            Assert.AreEqual((23, 23), (frame.Frame, frame.NextFrame));
            Assert.AreEqual(0f, frame.Blend);
        }

        [Test]
        public void FrameAt_BeforeTheStartShowsTheFirstFrame()
        {
            var frame = VATClip.Started(Walk, 10f).FrameAt(9f);

            Assert.AreEqual((10, 11, 0f), (frame.Frame, frame.NextFrame, frame.Blend));
        }

        [Test]
        public void Play_WritesOnlyWhenTheClipChanges()
        {
            var clip = VATClip.Started(Walk, 1f);

            Assert.IsFalse(clip.Play(Walk, 3f));
            Assert.AreEqual(1f, clip.Value.w);
            Assert.IsTrue(clip.Play(Fall, 3f));
            Assert.AreEqual(new float4(20f, 4f, -2f, 3f), clip.Value);
        }

        [Test]
        public void Import_ReadsLayoutClipsAndBounds()
        {
            var table = ScriptableObject.CreateInstance<VATClipTable>();
            try
            {
                table.Import(BakeInfoJson);

                var die = table.Range("Die");
                Assert.AreEqual((30, 21, 24f, false), (die.StartRow, die.FrameCount, die.Fps, die.Loop));
                Assert.AreEqual(0, table.Range("Idle").StartRow);
                Assert.AreEqual((1802, 2), (table.width, table.rowsPerFrame));
                Assert.That(math.distance(new float3(0f, 0.95f, 0.1f), table.Bounds.Center), Is.LessThan(1e-5f));
                Assert.That(math.distance(new float3(0.5f, 0.95f, 0.5f), table.Bounds.Extents), Is.LessThan(1e-5f));
                Assert.Throws<KeyNotFoundException>(() => table.Range("Run"));
            }
            finally
            {
                Object.DestroyImmediate(table);
            }
        }

        [Test]
        public void Import_RejectsAClipWithoutFrames()
        {
            var table = ScriptableObject.CreateInstance<VATClipTable>();
            try
            {
                var json = BakeInfoJson.Replace(@"""frameCount"": 21", @"""frameCount"": 0");
                Assert.Throws<InvalidDataException>(() => table.Import(json));
            }
            finally
            {
                Object.DestroyImmediate(table);
            }
        }
    }
}
