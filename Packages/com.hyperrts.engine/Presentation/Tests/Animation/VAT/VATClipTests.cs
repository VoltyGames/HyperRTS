using System.Collections.Generic;
using System.IO;
using HyperRTS.Presentation.Animation.Clips;
using HyperRTS.Presentation.Animation.VAT;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;

namespace HyperRTS.Presentation.Tests.Animation.VAT
{
    /// <summary>Vertex animation clips: row blending, looping and holding, clip changes and the bake info import.</summary>
    public class VATClipTests
    {
        private const string BakeInfoJson = @"{
            ""positions"": ""Pos.exr"",
            ""rotations"": ""Rot.exr"",
            ""width"": 1802,
            ""clips"": [
                { ""name"": ""Idle"", ""startRow"": 0, ""frameCount"": 30, ""fps"": 30, ""loop"": true },
                { ""name"": ""Die"", ""startRow"": 30, ""frameCount"": 21, ""fps"": 24, ""loop"": false }
            ],
            ""boundsMin"": [-0.5, 0, -0.4],
            ""boundsMax"": [0.5, 1.9, 0.6]
        }";

        private static readonly ClipRange Walk = new() { StartRow = 10, FrameCount = 4, Fps = 2f, Loop = true };
        private static readonly ClipRange Fall = new() { StartRow = 20, FrameCount = 4, Fps = 2f, Loop = false };

        [Test]
        public void BlendAt_BlendsBetweenFramesFromTheStartTime()
        {
            var frame = PlayingClip.Started(Walk, 5f).BlendAt(5.75f);

            Assert.AreEqual(11, frame.Row);
            Assert.AreEqual(12, frame.NextRow);
            Assert.AreEqual(0.5f, frame.Blend, 1e-5f);
        }

        [Test]
        public void BlendAt_LoopWrapsTheLastFrameIntoTheFirst()
        {
            var clip = PlayingClip.Started(Walk, 0f);

            var last = clip.BlendAt(1.75f);
            var wrapped = clip.BlendAt(2.25f);

            Assert.AreEqual((13, 10), (last.Row, last.NextRow));
            Assert.AreEqual((10, 11), (wrapped.Row, wrapped.NextRow));
        }

        [Test]
        public void BlendAt_OnceHoldsTheLastFrame()
        {
            var frame = PlayingClip.Started(Fall, 0f).BlendAt(100f);

            Assert.AreEqual((23, 23), (frame.Row, frame.NextRow));
            Assert.AreEqual(0f, frame.Blend);
        }

        [Test]
        public void BlendAt_BeforeTheStartShowsTheFirstFrame()
        {
            var frame = PlayingClip.Started(Walk, 10f).BlendAt(9f);

            Assert.AreEqual((10, 11, 0f), (frame.Row, frame.NextRow, frame.Blend));
        }

        [Test]
        public void Switch_FreezesTheShownPoseWithTheSwitchTime()
        {
            var clip = PlayingClip.Started(Walk, 5f);
            var fade = ClipFade.None;

            clip.Switch(Fall, 5.75f, 5.75f, 1f, ref fade);

            Assert.AreEqual(new float4(11f, 12f, 0.5f, 5.75f), fade.Value);
        }

        [Test]
        public void Import_ReadsWidthClipsAndBounds()
        {
            var table = ScriptableObject.CreateInstance<VATClipTable>();
            try
            {
                table.Import(BakeInfoJson);

                var die = table.Range("Die");
                Assert.AreEqual((30, 21, 24f, false), (die.StartRow, die.FrameCount, die.Fps, die.Loop));
                Assert.AreEqual(0, table.Range("Idle").StartRow);
                Assert.AreEqual(1802, table.width);
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

        [Test]
        public void Import_NeedsRotations()
        {
            var json = BakeInfoJson.Replace(@"""rotations"": ""Rot.exr"",", "");
            Assert.Throws<InvalidDataException>(() => VATBakeInfo.Parse(json, "House"));
        }
    }
}
